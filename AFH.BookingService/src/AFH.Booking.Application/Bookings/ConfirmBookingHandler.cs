using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Options;
using Common.Utilities;
using AFH.Booking.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Bookings;

public sealed class ConfirmBookingHandler : IConfirmBookingHandler
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _tx;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICalendarGateway _calendar;
    private readonly IMeetingLinkFactory _meetingLinks;
    private readonly BookingPortalOptions _portalOptions;

    public ConfirmBookingHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository tx,
        IUnitOfWork uow,
        IClock clock,
        ICalendarGateway calendar,
        IMeetingLinkFactory meetingLinks,
        IOptions<BookingPortalOptions> portalOptions)
    {
        _holds = holds;
        _slots = slots;
        _tx = tx;
        _uow = uow;
        _clock = clock;
        _calendar = calendar;
        _meetingLinks = meetingLinks;
        _portalOptions = portalOptions.Value;
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
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold already cancelled.", Errors.Conflict);

        if (hold.Status == BookingHoldStatus.Confirmed)
            return OkResponse(hold);

        if (hold.ExpiresUtc <= utcNow)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold has expired.", Errors.Conflict);

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold has no slotId.", Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' not found.", Errors.Conflict);

        var tx = await _tx.GetForUpdateAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<ConfirmBookingResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' not found.", Errors.Conflict);

        hold.Confirm(utcNow);
        await _holds.UpdateAsync(hold, ct);

        if (tx.Status == BookingTransactionStatus.Open)
        {
            tx.MarkCompleted();
            await _tx.UpdateAsync(tx, ct);
        }

        string? joinUrl = null;
        if (tx.IsRemote)
            joinUrl = await _meetingLinks.CreateJoinLinkAsync(hold.Id, ct);

        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            var windows = BuildHoldWindows(slot, tx);

            var body = ConfirmedBookingTemplate.BuildConfirmedBodyTemplate(
                slot: slot,
                tx: tx,
                booking: hold,
                windows: windows,
                joinUrl: joinUrl,
                location: null,
                cancelOrRearrangeUrl: BuildCancelOrRearrangeUrl(hold.Id, tx.TransactionRef, slot.AdviserId));

            var calendarEvent = BookingCalendarEvent.Update(
                userId: slot.AdviserId,
                providerEventId: hold.CalendarProviderEventId,
                showAs: BookingShowAs.Busy,
                body: body,
                categories: new[] { "AFH Booking", "Confirmed" });

            await _calendar.UpdateBookingEventAsync(calendarEvent, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return OkResponse(hold, joinUrl);
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
        var companyBufferMinutes = Math.Max(0, slot.CompanyBufferMinutes ?? 0);

        var preMeetingMinutes = travelMinutes + companyBufferMinutes;
        var postMeetingMinutes = companyBufferMinutes;

        var start = slot.StartUtc.AddMinutes(-preMeetingMinutes);
        var end = slot.EndUtc.AddMinutes(postMeetingMinutes);

        if (end <= start)
            return new HoldWindows(slot.StartUtc, slot.EndUtc, 0, 0, false);

        return new HoldWindows(start, end, travelMinutes, companyBufferMinutes, preMeetingMinutes > 0 || postMeetingMinutes > 0);
    }

    private string? BuildCancelOrRearrangeUrl(string bookingId, string transactionId, string adviserId)
    {
        if (string.IsNullOrWhiteSpace(_portalOptions.CancelOrRearrangeUrlTemplate))
            return null;

        return UrlTemplateHelper.Build(_portalOptions.CancelOrRearrangeUrlTemplate, new Dictionary<string, string>
        {
            ["bookingId"] = bookingId,
            ["transactionId"] = transactionId,
            ["adviserId"] = adviserId
        });
    }
}
