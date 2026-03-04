using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Bookings.Handlers;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;
using AFH.Booking.Domain.Bookings;
using Microsoft.Extensions.Logging;
using System.Net;

public sealed class CancelBookingHandler : ICancelBookingHandler
{
    private readonly IBookingRepository _repo;
    private readonly ICalendarService _calendar;
    private readonly ILogger<CancelBookingHandler> _logger;

    public CancelBookingHandler(IBookingRepository repo, ICalendarService calendar, ILogger<CancelBookingHandler> logger)
    {
        _repo = repo;
        _calendar = calendar;
        _logger = logger;
    }

    public async Task<Result<object>> HandleAsync(CancelBookingModel command, CancellationToken ct)
    {
        var booking = await _repo.GetAsync(new BookingId(command.BookingId), ct);
        if (booking is null) return Result<object>.NotFound("Booking not found");

        booking.Cancel();

        try
        {
            if (booking.ProviderEventId is not null)
                await _calendar.CancelEventAsync(booking.AdviserId, booking.ProviderEventId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel calendar event for BookingId={BookingId}", command.BookingId);
            return Result<object>.Fail(HttpStatusCode.InternalServerError, "Calendar cancellation failed", "CalendarError");
        }

        await _repo.SaveAsync(booking, ct);

        _logger.LogInformation("Booking cancelled. BookingId={BookingId}", booking.Id.Value);

        return Result<object>.Ok(new CancelBookingResponse
        {
            BookingId = booking.Id.Value,
            Cancelled = true
        });
    }
}
