using AFH.Booking.Application.Models.Calendar.Constants;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Calendar;

public sealed class BookingShowAsRemediationService : IBookingShowAsRemediationService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly ICalendarGateway _calendar;

    public BookingShowAsRemediationService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        ICalendarGateway calendar)
    {
        _holds = holds;
        _slots = slots;
        _calendar = calendar;
    }

    public async Task<Result<CalendarShowAsRemediationResult>> HandleAsync(string bookingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);

        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null)
            return Result<CalendarShowAsRemediationResult>.NotFound("Booking hold was not found.");

        if (string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Booking does not have a calendar event to remediate.",
                Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Booking slot was not found.",
                Errors.Conflict);

        var update = BookingCalendarEvent.Update(
            userId: slot.AdviserId,
            showAs: BookingShowAs.Busy,
            providerEventId: hold.CalendarProviderEventId,
            body: null,
            categories: CalendarCategoryConstants.ShowAsRemediation);

        await _calendar.UpdateBookingEventAsync(update, ct);

        return Result<CalendarShowAsRemediationResult>.Ok(new CalendarShowAsRemediationResult
        {
            BookingId = hold.Id,
            EventId = hold.CalendarProviderEventId,
            ShowAs = "Busy",
            RemediatedUtc = DateTime.UtcNow
        });
    }
}
