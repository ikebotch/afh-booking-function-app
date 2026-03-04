using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Bookings.Handlers;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;
using AFH.Booking.Domain.Bookings;
using Microsoft.Extensions.Logging;
using System.Net;

public sealed class ConfirmBookingHandler : IConfirmBookingHandler
{
    private readonly IBookingRepository _repo;
    private readonly ICalendarService _calendar;
    private readonly ILogger<ConfirmBookingHandler> _logger;

    public ConfirmBookingHandler(
        IBookingRepository repo,
        ICalendarService calendar,
        ILogger<ConfirmBookingHandler> logger)
    {
        _repo = repo;
        _calendar = calendar;
        _logger = logger;
    }

    public async Task<Result<object>> HandleAsync(ConfirmBookingModel command, CancellationToken ct)
    {
        var booking = await _repo.GetAsync(new BookingId(command.BookingId), ct);
        if (booking is null) return Result<object>.NotFound("Booking not found");

        booking.Confirm(DateTime.UtcNow);

        try
        {
            var providerEventId = await _calendar.CreateEventAsync(booking, ct);
            booking.AttachCalendarEvent(providerEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create calendar event for BookingId={BookingId}", command.BookingId);
            return Result<object>.Fail(HttpStatusCode.InternalServerError, "Calendar integration failed", "CalendarError");
        }

        await _repo.SaveAsync(booking, ct);

        _logger.LogInformation("Booking confirmed. BookingId={BookingId}", booking.Id.Value);

        return Result<object>.Ok(new ConfirmBookingResponse
        {
            BookingId = booking.Id.Value,
            ProviderEventId = booking.ProviderEventId,
            Status = "Confirmed"
        });
    }


}
