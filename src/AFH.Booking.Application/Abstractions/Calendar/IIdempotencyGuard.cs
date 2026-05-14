namespace AFH.Booking.Application.Abstractions.Calendar;

public interface IIdempotencyGuard
{
    Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct);
}