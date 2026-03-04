using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Bookings.Handlers;

public sealed class CreateHoldHandler : ICreateHoldHandler
{
    private readonly IBookingRepository _repo;
    private readonly ICalendarService _calendar;
    private readonly ILogger<CreateHoldHandler> _logger;

    public CreateHoldHandler(
        IBookingRepository repo,
        ICalendarService calendar,
        ILogger<CreateHoldHandler> logger)
    {
        _repo = repo;
        _calendar = calendar;
        _logger = logger;
    }

    public async Task<Result<object>> HandleAsync(CreateHoldModel command, CancellationToken ct)
    {
        var booking = command.Request.ToBookingModel(command.IdempotencyKey);
        var providerEventId = await _calendar.CreateEventAsync(booking, ct);

        _logger.LogInformation(
            "Hold created. BookingId={BookingId}, ProviderEventId={ProviderEventId}",
            booking.Id.Value,
            providerEventId);

        booking.AttachCalendarEvent(providerEventId);
        await _repo.SaveAsync(booking, ct);

        return Result<object>.Ok(new CreateHoldResponse
        {
            BookingId = booking.Id.Value,
            ProviderEventId = providerEventId
        });
    }
}
