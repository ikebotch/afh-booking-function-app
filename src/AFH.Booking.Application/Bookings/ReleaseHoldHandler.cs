using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;

public sealed class ReleaseHoldHandler : IReleaseHoldHandler
{
    private readonly IBookingHoldRepository _holds;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ReleaseHoldHandler(
        IBookingHoldRepository holds,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IUnitOfWork uow,
        IClock clock)
    {
        _holds = holds;
        _calendar = calendar;
        _profiles = profiles;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<ReleaseHoldResponse>> HandleAsync(string holdId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(holdId))
            return Result<ReleaseHoldResponse>.Fail(
                HttpStatusCode.BadRequest,
                "holdId is required.",
                "validation_error");

        var hold = await _holds.GetForUpdateAsync(holdId, ct);

        if (hold is null)
            return Result<ReleaseHoldResponse>.NotFound($"Hold '{holdId}' not found.");

        // Idempotent behaviour
        if (hold.Status == BookingHoldStatus.Cancelled ||
            hold.Status == BookingHoldStatus.Expired)
        {
            return Result<ReleaseHoldResponse>.Ok(new ReleaseHoldResponse
            {
                BookingId = hold.Id
            });
        }

        if (hold.Status == BookingHoldStatus.Confirmed)
            return Result<ReleaseHoldResponse>.Fail(
                HttpStatusCode.Conflict,
                "Confirmed holds cannot be released.",
                "conflict");

        // Cancel calendar event if it exists
        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            try
            {
                var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(hold.UserId, ct);
                await _calendar.CancelBookingEventAsync(
                    calendarUserId,
                    hold.CalendarProviderEventId,
                    ct);
            }
            catch
            {
            }
        }

        hold.Cancel("Released by user", _clock.UtcNow);

        await _holds.UpdateAsync(hold, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ReleaseHoldResponse>.Ok(new ReleaseHoldResponse
        {
            BookingId = hold.Id
        });
    }
}
