using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Abstractions.Persistence;
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

    public ConfirmBookingHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository tx,
        IUnitOfWork uow,
        IClock clock,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IMeetingLinkFactory meetingLinks,
        IBookingConflictService conflicts)
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

        hold.Confirm(utcNow);
        await _holds.UpdateAsync(hold, ct);

        if (tx.Status == BookingTransactionStatus.Open)
        {
            tx.MarkCompleted();
            await _tx.UpdateAsync(tx, ct);
        }

        Task<string?>? joinUrlTask = null;
        if (tx.IsRemote)
            joinUrlTask = _meetingLinks.CreateJoinLinkAsync(hold.Id, ct);

        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            var windows = BuildHoldWindows(slot, tx);
            var joinUrl = joinUrlTask is null ? null : await joinUrlTask;

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

        var finalJoinUrl = joinUrlTask is null ? null : await joinUrlTask;
        await _uow.SaveChangesAsync(ct);

        return OkResponse(hold, finalJoinUrl);
    }

    private static Result<ConfirmBookingResponse> OkResponse(BookingHold hold, string? joinUrl = null)
        => Result<ConfirmBookingResponse>.Ok(new ConfirmBookingResponse
        {
            BookingId = hold.Id,
            SlotId = hold.SlotId,
            Status = BookingHoldStatus.Confirmed.ToString(),
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
}
