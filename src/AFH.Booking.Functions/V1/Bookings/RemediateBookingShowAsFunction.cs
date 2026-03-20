using AFH.Booking.Domain.Calendar;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class RemediateBookingShowAsFunction
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly ICalendarGateway _calendar;

    public RemediateBookingShowAsFunction(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        ICalendarGateway calendar)
    {
        _holds = holds;
        _slots = slots;
        _calendar = calendar;
    }

    [Function("Bookings_RemediateShowAs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/calendar/remediate-showas")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "Validation");

        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Booking hold was not found.", ct, "NotFound");

        if (string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
            return await req.ProblemAsync(HttpStatusCode.Conflict, "Booking does not have a calendar event to remediate.", ct, "Conflict");

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return await req.ProblemAsync(HttpStatusCode.Conflict, "Booking slot was not found.", ct, "Conflict");

        var update = BookingCalendarEvent.Update(
            userId: slot.AdviserId,
            showAs: BookingShowAs.Busy,
            providerEventId: hold.CalendarProviderEventId,
            body: null,
            categories: new[] { "AFH Booking", "Confirmed", "ShowAsRemediated" });

        await _calendar.UpdateBookingEventAsync(update, ct);

        return await req.OkJsonAsync(new
        {
            bookingId = hold.Id,
            eventId = hold.CalendarProviderEventId,
            showAs = "Busy",
            remediatedUtc = DateTime.UtcNow
        }, ct);
    }
}
