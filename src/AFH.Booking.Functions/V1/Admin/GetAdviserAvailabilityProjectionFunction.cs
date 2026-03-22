using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Options;
using AFH.Booking.Functions.Http;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Functions.V1.Admin;

public sealed class GetAdviserAvailabilityProjectionFunction
{
    private readonly IAdviserAvailabilityProjectionRepository _repo;
    private readonly CalendarProjectionOptions _options;

    public GetAdviserAvailabilityProjectionFunction(
        IAdviserAvailabilityProjectionRepository repo,
        IOptions<CalendarProjectionOptions> options)
    {
        _repo = repo;
        _options = options.Value;
    }

    [Function("Admin_GetAdviserAvailabilityProjection")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/advisers/{adviserId}/availability-projection")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adviserId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "adviserId is required.", ct, "Validation");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (!DateTime.TryParse(query.Get("startUtc"), out var startUtc) ||
            !DateTime.TryParse(query.Get("endUtc"), out var endUtc))
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "startUtc and endUtc query values are required.", ct, "Validation");
        }

        if (endUtc <= startUtc)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "endUtc must be greater than startUtc.", ct, "Validation");

        startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        endUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);

        var blocks = await _repo.ListBusyBlocksAsync(adviserId, startUtc, endUtc, ct);
        var lastSyncedUtc = await _repo.GetLastSyncedUtcAsync(adviserId, ct);

        var staleAfterMinutes = Math.Max(1, _options.StaleAfterMinutes);
        var isStale = !lastSyncedUtc.HasValue || (DateTime.UtcNow - lastSyncedUtc.Value).TotalMinutes > staleAfterMinutes;

        return await req.OkJsonAsync(new
        {
            adviserId,
            startUtc,
            endUtc,
            projection = new
            {
                isStale,
                staleAfterMinutes,
                lastSyncedUtc,
                blocks
            }
        }, ct);
    }
}
