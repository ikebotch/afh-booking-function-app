using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Admin;

[BookingOpenApiTag("Internal/Admin")]
public sealed class GetAdviserProjectionFeedFunction
{
    private readonly IAdviserProfileProjectionRepository _profiles;

    public GetAdviserProjectionFeedFunction(IAdviserProfileProjectionRepository profiles)
    {
        _profiles = profiles;
    }

    [Function("Admin_GetAdviserProjectionFeed")]
    [BookingOpenApiQueryParameter("sinceUtc", "string", Format = "date-time", Description = "Only return projections updated at or after this UTC timestamp.")]
    [BookingOpenApiQueryParameter("take", "integer", Description = "Maximum number of advisers to return.")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/advisers/projection/feed")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        DateTime? sinceUtc = null;
        if (DateTime.TryParse(query.Get("sinceUtc"), out var parsedSince))
            sinceUtc = DateTime.SpecifyKind(parsedSince, DateTimeKind.Utc);

        var take = 500;
        if (int.TryParse(query.Get("take"), out var parsedTake))
            take = parsedTake;

        var advisers = await _profiles.ListAsync(sinceUtc, take, ct);
        var mapped = advisers.Select(x => new
        {
            adviserId = x.AdviserId,
            displayName = x.DisplayName,
            mailboxUserId = x.MailboxUserId,
            homePostcode = x.HomePostcode,
            region = x.Region,
            skills = x.Skills,
            rating = x.Rating,
            isActive = x.IsActive,
            coverageRadiusMiles = x.CoverageRadiusMiles,
            maxTravelTimeMinutes = x.MaxTravelTimeMinutes,
            lastSyncedUtc = x.LastSyncedUtc,
            sourceVersion = x.SourceVersion
        }).ToList();

        return await req.OkJsonAsync(new
        {
            advisers = mapped,
            sync = new
            {
                sinceUtc,
                returned = mapped.Count,
                generatedAtUtc = DateTime.UtcNow
            }
        }, ct, HttpResponseExtensions.SinglePage(mapped.Count));
    }
}
