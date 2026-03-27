using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Options;
using AFH.Booking.Functions.Http;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Functions.V1.Calendar;

[BookingOpenApiTag("Calendar")]
public sealed class ProjectedScheduleBatchFunction
{
    private readonly IAdviserAvailabilityProjectionRepository _projection;
    private readonly CalendarProjectionOptions _options;

    public ProjectedScheduleBatchFunction(
        IAdviserAvailabilityProjectionRepository projection,
        IOptions<CalendarProjectionOptions> options)
    {
        _projection = projection;
        _options = options.Value;
    }

    [Function("Calendar_ProjectedScheduleBatch")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/calendar/users/schedule/batch")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<ProjectedBatchRequest>(ct);
        if (payload is null || payload.UserIds.Count == 0 || payload.EndUtc <= payload.StartUtc)
        {
            return await req.ProblemAsync(
                HttpStatusCode.BadRequest,
                "userIds/startUtc/endUtc are required and endUtc must be greater than startUtc.",
                ct,
                "Validation");
        }

        var users = payload.UserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var schedules = new List<object>(users.Count);
        foreach (var userId in users)
        {
            var blocks = await _projection.ListBusyBlocksAsync(userId, payload.StartUtc, payload.EndUtc, ct);
            var lastSyncedUtc = await _projection.GetLastSyncedUtcAsync(userId, ct);
            var staleAfterMinutes = Math.Max(1, _options.StaleAfterMinutes);
            var isStale = !lastSyncedUtc.HasValue || (DateTime.UtcNow - lastSyncedUtc.Value).TotalMinutes > staleAfterMinutes;
            var bookings = blocks.Select(x => new
            {
                bookingId = x.ProviderEventId,
                subject = x.Subject ?? "Busy",
                startUtc = x.StartUtc,
                endUtc = x.EndUtc,
                status = "Busy"
            }).ToList();

            schedules.Add(new
            {
                userId,
                state = "Ok",
                message = (string?)null,
                bookings,
                projection = new
                {
                    isStale,
                    staleAfterMinutes,
                    lastSyncedUtc
                }
            });
        }

        return await req.OkJsonAsync(new
        {
            startUtc = payload.StartUtc,
            endUtc = payload.EndUtc,
            schedules
        }, ct);
    }

    private sealed class ProjectedBatchRequest
    {
        public List<string> UserIds { get; set; } = new();
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }
}
