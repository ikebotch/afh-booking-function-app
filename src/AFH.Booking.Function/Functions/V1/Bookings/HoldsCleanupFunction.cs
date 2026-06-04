using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Bookings.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Function.Functions.V1.Bookings;

public sealed class HoldsCleanupFunction
{
    private readonly IBookingHoldRepository _holds;
    private readonly IReleaseHoldService _release;
    private readonly IClock _clock;
    private readonly ILogger<HoldsCleanupFunction> _logger;

    public HoldsCleanupFunction(
        IBookingHoldRepository holds,
        IReleaseHoldService release,
        IClock clock,
        ILogger<HoldsCleanupFunction> logger)
    {
        _holds = holds;
        _release = release;
        _clock = clock;
        _logger = logger;
    }

    // every 2 minutes
    [Function("Holds_Cleanup")]
    public async Task Run(
        [TimerTrigger("0 */2 * * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var utcNow = _clock.UtcNow;

        var expired = await _holds.GetExpiredActiveAsync(utcNow, take: 200, ct);

        if (expired.Count == 0)
        {
            _logger.LogInformation("Holds cleanup: none expired.");
            return;
        }

        _logger.LogInformation("Holds cleanup: releasing {Count} expired holds.", expired.Count);

        var released = 0;
        var failed = 0;

        foreach (var hold in expired)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var result = await _release.HandleAsync(new ReleaseHoldCommand
                {
                    HoldId = hold.Id,
                    ActorContext = BookingActorContext.SystemJob("HoldsCleanup")
                }, ct);

                if (result.IsSuccess)
                    released++;
                else
                    failed++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed releasing hold {HoldId}", hold.Id);
            }
        }

        var durationMs = (DateTime.UtcNow - started).TotalMilliseconds;

        _logger.LogInformation(
            "Holds cleanup completed. Processed={Processed}, Released={Released}, Failed={Failed}, DurationMs={Duration}",
            expired.Count,
            released,
            failed,
            durationMs);
    }
}
