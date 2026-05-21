using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Services.AdviserProjection;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Holds;

public sealed class ConfirmBookingService : IConfirmBookingService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _tx;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IMeetingLinkFactory _meetingLinks;
    private readonly IBookingConflictService _conflicts;
    private readonly ISelectedSlotRouteTimeGuard _routeTimeGuard;
    private readonly ILifecycleAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IHoldWindowFactory _holdWindowFactory;

    public ConfirmBookingService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository tx,
        IUnitOfWork uow,
        IClock clock,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IMeetingLinkFactory meetingLinks,
        IBookingConflictService conflicts,
        ISelectedSlotRouteTimeGuard routeTimeGuard,
        ILifecycleAuditService audit,
        INotificationService notifications,
        IHoldWindowFactory holdWindowFactory)
    {
        _holds = holds;
        _slots = slots;
        _tx = tx;
        _uow = uow;
        _clock = clock;
        _calendar = calendar;
        _profiles = profiles;
        _meetingLinks = meetingLinks;
        _conflicts = conflicts;
        _routeTimeGuard = routeTimeGuard;
        _audit = audit;
        _notifications = notifications;
        _holdWindowFactory = holdWindowFactory;
    }

    public async Task<Result<ConfirmBookingResponse>> HandleAsync(
        ConfirmBookingCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.HoldId))
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.BadRequest,
                "holdId is required.",
                Errors.Validation);
        }

        var utcNow = _clock.UtcNow;

        var hold = await _holds.GetForUpdateAsync(cmd.HoldId.Trim(), ct);
        if (hold is null)
        {
            return Result<ConfirmBookingResponse>.NotFound(
                $"Hold '{cmd.HoldId}' not found.");
        }

        if (hold.Status == BookingHoldStatus.Cancelled)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Hold already cancelled.",
                Errors.HoldCancelled);
        }

        if (hold.Status == BookingHoldStatus.Confirmed)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Hold already confirmed.",
                Errors.HoldAlreadyConfirmed);
        }

        if (hold.ExpiresUtc <= utcNow)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Hold has expired.",
                Errors.HoldExpired);
        }

        if (string.IsNullOrWhiteSpace(hold.SlotId))
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Hold has no slotId.",
                Errors.HoldStateInvalid);
        }

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Slot '{hold.SlotId}' not found.",
                Errors.HoldSlotMissing);
        }

        var tx = await _tx.GetForUpdateAsync(slot.TransactionId, ct);
        if (tx is null)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' not found.",
                Errors.HoldTransactionMissing);
        }

        var routeTimeCheck = await _routeTimeGuard.EvaluateAsync(slot, tx, hold.Id, ct);
        if (!routeTimeCheck.IsAllowed)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                routeTimeCheck.ErrorMessage
                    ?? "The selected slot is no longer available.",
                routeTimeCheck.ErrorCode ?? Errors.ExactRouteTimeUnavailable);
        }

        if (routeTimeCheck.WasTriggered &&
            routeTimeCheck.TravelTimeMinutes.HasValue &&
            routeTimeCheck.TravelDistanceMiles.HasValue)
        {
            slot.AttachTravelSnapshot(
                travelMinutes: routeTimeCheck.TravelTimeMinutes,
                distanceMiles: routeTimeCheck.TravelDistanceMiles,
                companyBufferMinutes: slot.CompanyBufferMinutes,
                sourceLocationRef: slot.SourceLocationRef,
                sourcePostcode: slot.SourcePostcode,
                sourceLatitude: slot.SourceLatitude,
                sourceLongitude: slot.SourceLongitude,
                destinationLocationRef: slot.DestinationLocationRef,
                destinationPostcode: slot.DestinationPostcode,
                destinationLatitude: slot.DestinationLatitude,
                destinationLongitude: slot.DestinationLongitude,
                provider: "LocationRouteTime",
                confidence: "Exact",
                calculatedUtc: utcNow);

            await _slots.UpdateAsync(slot, ct);
        }

        var calendarUserId =
            await _profiles.ResolveCalendarUserIdAsync(slot.AdviserId, ct);

        var conflicts =
            await _conflicts.EvaluateConfirmationConflictsAsync(
                hold,
                slot,
                tx,
                calendarUserId,
                ct);

        if (conflicts.IsBlocked)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                conflicts.ErrorMessage
                    ?? "Booking confirmation blocked by calendar conflict.",
                conflicts.ErrorCode ?? Errors.Conflict);
        }

        var before = CreateSnapshot(hold, slot, tx);

        hold.Confirm(utcNow);
        await _holds.UpdateAsync(hold, ct);

        if (tx.Status == BookingTransactionStatus.Open)
        {
            tx.MarkCompleted();
            await _tx.UpdateAsync(tx, ct);
        }

        string? joinUrl = null;
        if (tx.IsRemote)
        {
            joinUrl = await _meetingLinks.CreateJoinLinkAsync(hold.Id, ct);
        }

        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            var windows = _holdWindowFactory.Create(slot, tx);

            var calendarTemplate =
                ConfirmedBookingTemplate.BuildConfirmedTemplate(
                    slot: slot,
                    tx: tx,
                    booking: hold,
                    windows: windows,
                    joinUrl: joinUrl,
                    location: null);

            var calendarEvent = BookingCalendarEvent.Update(
                userId: calendarUserId,
                providerEventId: hold.CalendarProviderEventId,
                showAs: BookingShowAs.Busy,
                body: calendarTemplate.CalendarDescription,
                categories: new[] { "AFH Booking", "Confirmed" });

            await _calendar.UpdateBookingEventAsync(calendarEvent, ct);
        }

        var eventId = await _audit.RecordEventAsync(
            new LifecycleAuditEntry(
                BookingId: hold.Id,
                TransactionId: tx.Id,
                EventType: LifecycleEventTypes.Booked,
                ActorType: LifecycleActors.Client,
                ActorId: null,
                ReasonCode: null,
                ReasonNotes: cmd.Notes,
                Before: before,
                After: CreateSnapshot(hold, slot, tx),
                OccurredUtc: utcNow,
                CorrelationId: null,
                SourceSystem: "BookingService",
                RelatedBookingId: null,
                PreviousState: null,
                NewState: LifecycleStates.Booked),
            ct);

        await _audit.RecordStepAsync(
            new LifecycleAuditStepEntry(
                eventId,
                LifecycleStepNames.Outlook,
                1,
                string.IsNullOrWhiteSpace(hold.CalendarProviderEventId)
                    ? LifecycleStepStatuses.Skipped
                    : LifecycleStepStatuses.Succeeded,
                utcNow,
                _clock.UtcNow),
            ct);

        await _audit.RecordStepAsync(
            new LifecycleAuditStepEntry(
                eventId,
                LifecycleStepNames.SqlAudit,
                2,
                LifecycleStepStatuses.Succeeded,
                utcNow,
                _clock.UtcNow),
            ct);

        await _uow.SaveChangesAsync(ct);

        var notificationStartedUtc = _clock.UtcNow;
        var notificationStatus = LifecycleStepStatuses.Succeeded;
        string? notificationErrorCode = null;
        string? notificationErrorDetails = null;

        try
        {
            await _notifications.SendBookingNotificationAsync(
                new NotificationDispatchRequest(
                    hold.Id,
                    LifecycleEventTypes.Booked,
                    BuildBookingConfirmationMessage(slot),
                    true,
                    true,
                    eventId,
                    null),
                ct);
        }
        catch (Exception ex)
        {
            notificationStatus = LifecycleStepStatuses.Failed;
            notificationErrorCode = LifecycleErrorCodes.NotificationFailed;
            notificationErrorDetails = ex.Message;
        }

        await _audit.RecordStepAsync(
            new LifecycleAuditStepEntry(
                eventId,
                LifecycleStepNames.Notifications,
                3,
                notificationStatus,
                notificationStartedUtc,
                _clock.UtcNow,
                notificationErrorCode,
                notificationErrorDetails),
            ct);

        await _uow.SaveChangesAsync(ct);

        return OkResponse(hold, tx, joinUrl);
    }

    private static Result<ConfirmBookingResponse> OkResponse(
        BookingHold hold,
        BookingTransaction tx,
        string? joinUrl = null)
    {
        return Result<ConfirmBookingResponse>.Ok(
            new ConfirmBookingResponse
            {
                BookingId = hold.Id,
                SlotId = hold.SlotId,
                TransactionId = tx.Id,
                TransactionRef = tx.TransactionRef,
                Status = BookingHoldStatus.Confirmed.ToString(),
                LifecycleState = LifecycleEventTypes.Booked,
                OnlineMeetingJoinUrl = joinUrl
            });
    }

    private static object CreateSnapshot(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction tx)
    {
        return new
        {
            bookingId = hold.Id,
            lifecycleState = hold.Status == BookingHoldStatus.Confirmed
                ? LifecycleEventTypes.Booked
                : hold.Status.ToString(),
            holdStatus = hold.Status.ToString(),
            holdConfirmedUtc = hold.ConfirmedUtc,
            slotId = slot.Id,
            slotStartUtc = slot.StartUtc,
            slotEndUtc = slot.EndUtc,
            adviserId = slot.AdviserId,
            transactionId = tx.Id,
            transactionRef = tx.TransactionRef,
            transactionStatus = tx.Status.ToString()
        };
    }

    private static string BuildBookingConfirmationMessage(BookingSlot slot)
    {
        return
            $"Your meeting with {slot.AdviserName} on {slot.StartUtc:yyyy-MM-dd HH:mm} has been booked.";
    }
}
