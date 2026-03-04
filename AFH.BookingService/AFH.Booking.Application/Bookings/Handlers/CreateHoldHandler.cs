using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings.Commands;
using AFH.Booking.Application.Common;
using AFH.Common.CalendarUtils.Sdk.Services.Abstractions;
using Microsoft.Extensions.Logging;


namespace AFH.Booking.Application.Bookings.Handlers;

public sealed class CreateHoldHandler : ICreateHoldHandler
{
    private readonly IBookingRepository _repo;
    private readonly ICalendarClient _calendar;

    private readonly ILogger<CreateHoldHandler> _logger;

    public CreateHoldHandler(
        IBookingRepository repo,
        ICalendarClient calendar,

        ILogger<CreateHoldHandler> logger)
    {
        _repo = repo;
        _calendar = calendar;

        _logger = logger;
    }


    public async Task<Result<object>> HandleAsync(CreateHoldModel command, CancellationToken ct)
    {
        var booking = command.Request.ToBookingModel(command.IdempotencyKey);

        var upsertRequest = booking.ToUpsertCalendarEventRequest();

        var upsert = await _calendar.UpsertAsync(upsertRequest, ct);

        _logger.LogInformation(
            "Hold created. BookingId={BookingId}, ProviderEventId={ProviderEventId}",
            booking.Id.Value,
            upsert.ProviderEventId);

        booking.AttachCalendarEvent(upsert.ProviderEventId);
        await _repo.SaveAsync(booking, ct);

        return Result<object>.Ok(upsert.ToCreateHoldResponse(booking.Id.Value));
    }

}
