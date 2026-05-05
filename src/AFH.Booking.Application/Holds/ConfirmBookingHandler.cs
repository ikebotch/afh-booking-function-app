using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Bookings;

public sealed class ConfirmBookingHandler : IConfirmBookingHandler
{
    private const int DefaultCompanyBufferMinutes = 30;
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _tx;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IMeetingLinkFactory _meetingLinks;
    private readonly IBookingConflictService _conflicts;
    private readonly ILifecycleAuditService _audit;
    private readonly INotificationService _notifications;

    public ConfirmBookingHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository tx,
        IUnitOfWork uow,
        IClock clock,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IMeetingLinkFactory meetingLinks,
        IBookingConflictService conflicts,
        ILifecycleAuditService audit,
        INotificationService notifications)
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
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<Result<ConfirmBookingResponse>> HandleAsync(ConfirmBookingCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.HoldId))
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.BadRequest, "holdId is required.", Errors.Validation);

        var utcNow = _clock.UtcNow;

        var hold = await _holds.GetForUpdateAsync(cmd.HoldId.Trim(), ct);
        if (hold is null)
            return Result<ConfirmBookingResponse>.NotFound($"Hold '{cmd.HoldId}' not found.");

        if (hold.Status == BookingHoldStatus.Cancelled)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold already cancelled.", Errors.HoldCancelled);

        if (hold.Status == BookingHoldStatus.Confirmed)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold already confirmed.", Errors.HoldAlreadyConfirmed);

        if (hold.ExpiresUtc <= utcNow)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold has expired.", Errors.HoldExpired);

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold has no slotId.", Errors.HoldStateInvalid);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' not found.", Errors.HoldSlotMissing);

        var tx = await _tx.GetForUpdateAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' not found.", Errors.HoldTransactionMissing);

        var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(slot.AdviserId, ct);

        var conflicts = await _conflicts.EvaluateConfirmationConflictsAsync(hold, slot, tx, calendarUserId, ct);
        if (conflicts.IsBlocked)
        {
            return Result<ConfirmBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                conflicts.ErrorMessage ?? "Booking confirmation blocked by calendar conflict.",
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

        // Meeting-link creation may require loading additional booking/client data.
        // Keep this sequential to avoid overlapping EF-backed operations on the scoped DbContext.
        string? joinUrl = null;
        if (tx.IsRemote)
            joinUrl = await _meetingLinks.CreateJoinLinkAsync(hold.Id, ct);

        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            var windows = BuildHoldWindows(slot, tx);

            var calendarTemplate = ConfirmedBookingTemplate.BuildConfirmedTemplate(
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

        var eventId = await _audit.RecordEventAsync(new LifecycleAuditEntry(
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
            RelatedBookingId: null), ct);

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.Outlook,
            1,
            string.IsNullOrWhiteSpace(hold.CalendarProviderEventId)
                ? LifecycleStepStatuses.Skipped
                : LifecycleStepStatuses.Succeeded,
            utcNow,
            _clock.UtcNow), ct);

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.SqlAudit,
            2,
            LifecycleStepStatuses.Succeeded,
            utcNow,
            _clock.UtcNow), ct);

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

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.Notifications,
            3,
            notificationStatus,
            notificationStartedUtc,
            _clock.UtcNow,
            notificationErrorCode,
            notificationErrorDetails), ct);

        await _uow.SaveChangesAsync(ct);

        return OkResponse(hold, tx, joinUrl);
    }

    private static Result<ConfirmBookingResponse> OkResponse(BookingHold hold, BookingTransaction tx, string? joinUrl = null)
        => Result<ConfirmBookingResponse>.Ok(new ConfirmBookingResponse
        {
            BookingId = hold.Id,
            SlotId = hold.SlotId,
            TransactionId = tx.Id,
            TransactionRef = tx.TransactionRef,
            Status = BookingHoldStatus.Confirmed.ToString(),
            LifecycleState = LifecycleEventTypes.Booked,
            OnlineMeetingJoinUrl = joinUrl
        });

    private static HoldWindows BuildHoldWindows(BookingSlot slot, BookingTransaction tx)
    {
        var travelMinutes = tx.IsRemote ? 0 : Math.Max(0, slot.TravelMinutes ?? 0);
        var companyBufferMinutes = tx.IsRemote
            ? 0
            : Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes);

        var preMeetingMinutes = travelMinutes + companyBufferMinutes;
        var postMeetingMinutes = companyBufferMinutes;

        var start = slot.StartUtc.AddMinutes(-preMeetingMinutes);
        var end = slot.EndUtc.AddMinutes(+postMeetingMinutes);

        if (end <= start)
            return new HoldWindows(slot.StartUtc, slot.EndUtc, 0, 0, false);

        return new HoldWindows(start, end, travelMinutes, companyBufferMinutes, preMeetingMinutes > 0 || postMeetingMinutes > 0);
    }

    private static object CreateSnapshot(BookingHold hold, BookingSlot slot, BookingTransaction tx)
    {
        return new
        {
            bookingId = hold.Id,
            lifecycleState = hold.Status == BookingHoldStatus.Confirmed ? LifecycleEventTypes.Booked : hold.Status.ToString(),
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
        => $"Your meeting with {slot.AdviserName} on {slot.StartUtc:yyyy-MM-dd HH:mm} has been booked.";
}
