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
        var utcNow = _clock.UtcNow;

        var contextResult = await LoadConfirmationContextAsync(cmd, utcNow, ct);
        if (!contextResult.IsSuccess || contextResult.Value is null)
            return FailLike<ConfirmationContext, ConfirmBookingResponse>(contextResult);

        var context = contextResult.Value;

        var routeTimeResult = await ApplyRouteTimeSnapshotIfRequiredAsync(context, utcNow, ct);
        if (!routeTimeResult.IsSuccess)
            return FailLike<ConfirmBookingResponse>(routeTimeResult);

        var calendarUserIdResult = await ResolveCalendarUserAndCheckConflictsAsync(context, ct);
        if (!calendarUserIdResult.IsSuccess || calendarUserIdResult.Value is null)
            return FailLike<string, ConfirmBookingResponse>(calendarUserIdResult);

        var before = CreateSnapshot(context.Hold, context.Slot, context.Transaction);
        await ConfirmHoldAndTransactionAsync(context, utcNow, ct);

        var joinUrl = await CreateJoinLinkIfRemoteAsync(context, ct);
        await UpdateConfirmedCalendarEventAsync(context, calendarUserIdResult.Value, joinUrl, ct);

        var eventId = await RecordBookedLifecycleAsync(cmd, context, before, utcNow, ct);
        await _uow.SaveChangesAsync(ct);

        await SendBookedNotificationAsync(context.Hold.Id, context.Slot, eventId, ct);
        await _uow.SaveChangesAsync(ct);

        return OkResponse(context.Hold, context.Transaction, joinUrl);
    }

    private async Task<Result<ConfirmationContext>> LoadConfirmationContextAsync(
        ConfirmBookingCommand cmd,
        DateTime utcNow,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.HoldId))
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.BadRequest,
                "holdId is required.",
                Errors.Validation);
        }

        var hold = await _holds.GetForUpdateAsync(cmd.HoldId.Trim(), ct);
        if (hold is null)
            return Result<ConfirmationContext>.NotFound($"Hold '{cmd.HoldId}' not found.");

        var holdStatusResult = ValidateHoldCanBeConfirmed(hold, utcNow);
        if (!holdStatusResult.IsSuccess)
            return FailLike<ConfirmationContext>(holdStatusResult);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.Conflict,
                $"Slot '{hold.SlotId}' not found.",
                Errors.HoldSlotMissing);
        }

        var tx = await _tx.GetForUpdateAsync(slot.TransactionId, ct);
        if (tx is null)
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' not found.",
                Errors.HoldTransactionMissing);
        }

        return Result<ConfirmationContext>.Ok(new ConfirmationContext(hold, slot, tx));
    }

    private static Result ValidateHoldCanBeConfirmed(BookingHold hold, DateTime utcNow)
    {
        if (hold.Status == BookingHoldStatus.Cancelled)
            return Result.Fail(HttpStatusCode.Conflict, "Hold already cancelled.", Errors.HoldCancelled);

        if (hold.Status == BookingHoldStatus.Confirmed)
            return Result.Fail(HttpStatusCode.Conflict, "Hold already confirmed.", Errors.HoldAlreadyConfirmed);

        if (hold.ExpiresUtc <= utcNow)
            return Result.Fail(HttpStatusCode.Conflict, "Hold has expired.", Errors.HoldExpired);

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result.Fail(HttpStatusCode.Conflict, "Hold has no slotId.", Errors.HoldStateInvalid);

        return Result.Ok();
    }

    private async Task<Result> ApplyRouteTimeSnapshotIfRequiredAsync(
        ConfirmationContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        var routeTimeCheck = await _routeTimeGuard.EvaluateAsync(
            context.Slot,
            context.Transaction,
            context.Hold.Id,
            ct);

        if (!routeTimeCheck.IsAllowed)
        {
            return Result.Fail(
                HttpStatusCode.Conflict,
                routeTimeCheck.ErrorMessage ?? "The selected slot is no longer available.",
                routeTimeCheck.ErrorCode ?? Errors.ExactRouteTimeUnavailable);
        }

        if (!routeTimeCheck.WasTriggered ||
            !routeTimeCheck.TravelTimeMinutes.HasValue ||
            !routeTimeCheck.TravelDistanceMiles.HasValue)
        {
            return Result.Ok();
        }

        context.Slot.AttachTravelSnapshot(
            travelMinutes: routeTimeCheck.TravelTimeMinutes,
            distanceMiles: routeTimeCheck.TravelDistanceMiles,
            companyBufferMinutes: context.Slot.CompanyBufferMinutes,
            sourceLocationRef: context.Slot.SourceLocationRef,
            sourcePostcode: context.Slot.SourcePostcode,
            sourceLatitude: context.Slot.SourceLatitude,
            sourceLongitude: context.Slot.SourceLongitude,
            destinationLocationRef: context.Slot.DestinationLocationRef,
            destinationPostcode: context.Slot.DestinationPostcode,
            destinationLatitude: context.Slot.DestinationLatitude,
            destinationLongitude: context.Slot.DestinationLongitude,
            provider: "LocationRouteTime",
            confidence: "Exact",
            calculatedUtc: utcNow);

        await _slots.UpdateAsync(context.Slot, ct);
        return Result.Ok();
    }

    private async Task<Result<string>> ResolveCalendarUserAndCheckConflictsAsync(
        ConfirmationContext context,
        CancellationToken ct)
    {
        var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(context.Slot.AdviserId, ct);

        var conflicts = await _conflicts.EvaluateConfirmationConflictsAsync(
            context.Hold,
            context.Slot,
            context.Transaction,
            calendarUserId,
            ct);

        if (conflicts.IsBlocked)
        {
            return Result<string>.Fail(
                HttpStatusCode.Conflict,
                conflicts.ErrorMessage ?? "Booking confirmation blocked by calendar conflict.",
                conflicts.ErrorCode ?? Errors.Conflict);
        }

        return Result<string>.Ok(calendarUserId);
    }

    private async Task ConfirmHoldAndTransactionAsync(
        ConfirmationContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        context.Hold.Confirm(utcNow);
        await _holds.UpdateAsync(context.Hold, ct);

        if (context.Transaction.Status != BookingTransactionStatus.Open)
            return;

        context.Transaction.MarkCompleted();
        await _tx.UpdateAsync(context.Transaction, ct);
    }

    private async Task<string?> CreateJoinLinkIfRemoteAsync(
        ConfirmationContext context,
        CancellationToken ct)
    {
        return context.Transaction.IsRemote
            ? await _meetingLinks.CreateJoinLinkAsync(context.Hold.Id, ct)
            : null;
    }

    private async Task UpdateConfirmedCalendarEventAsync(
        ConfirmationContext context,
        string calendarUserId,
        string? joinUrl,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Hold.CalendarProviderEventId))
            return;

        var windows = _holdWindowFactory.Create(context.Slot, context.Transaction);
        var calendarTemplate = ConfirmedBookingTemplate.BuildConfirmedTemplate(
            slot: context.Slot,
            tx: context.Transaction,
            booking: context.Hold,
            windows: windows,
            joinUrl: joinUrl,
            location: null);

        var calendarEvent = BookingCalendarEvent.Update(
            userId: calendarUserId,
            providerEventId: context.Hold.CalendarProviderEventId,
            showAs: BookingShowAs.Busy,
            body: calendarTemplate.CalendarDescription,
            categories: new[] { "AFH Booking", "Confirmed" });

        await _calendar.UpdateBookingEventAsync(calendarEvent, ct);
    }

    private async Task<string> RecordBookedLifecycleAsync(
        ConfirmBookingCommand cmd,
        ConfirmationContext context,
        object before,
        DateTime utcNow,
        CancellationToken ct)
    {
        var eventId = await _audit.RecordEventAsync(
            new LifecycleAuditEntry(
                BookingId: context.Hold.Id,
                TransactionId: context.Transaction.Id,
                EventType: LifecycleEventTypes.Booked,
                ActorType: LifecycleActors.Client,
                ActorId: null,
                ReasonCode: null,
                ReasonNotes: cmd.Notes,
                Before: before,
                After: CreateSnapshot(context.Hold, context.Slot, context.Transaction),
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
                string.IsNullOrWhiteSpace(context.Hold.CalendarProviderEventId)
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

        return eventId;
    }

    private async Task SendBookedNotificationAsync(
        string bookingId,
        BookingSlot slot,
        string eventId,
        CancellationToken ct)
    {
        var notificationStartedUtc = _clock.UtcNow;
        var notificationStatus = LifecycleStepStatuses.Succeeded;
        string? notificationErrorCode = null;
        string? notificationErrorDetails = null;

        try
        {
            await _notifications.SendBookingNotificationAsync(
                new NotificationDispatchRequest(
                    bookingId,
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

    private static Result<T> FailLike<T>(Result failure)
    {
        return Result<T>.Fail(
            failure.StatusCode,
            failure.ErrorMessage ?? "Request failed.",
            failure.ErrorCode);
    }

    private static Result<TTo> FailLike<TFrom, TTo>(Result<TFrom> failure)
    {
        return Result<TTo>.Fail(
            failure.StatusCode,
            failure.ErrorMessage ?? "Request failed.",
            failure.ErrorCode);
    }

    private sealed record ConfirmationContext(
        BookingHold Hold,
        BookingSlot Slot,
        BookingTransaction Transaction);
}
