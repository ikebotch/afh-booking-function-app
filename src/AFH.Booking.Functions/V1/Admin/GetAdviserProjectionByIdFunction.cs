using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Admin;

[BookingOpenApiTag("Internal/Admin")]
public sealed class GetAdviserProjectionByIdFunction
{
    private readonly IAdviserProfileProjectionRepository _profiles;

    public GetAdviserProjectionByIdFunction(IAdviserProfileProjectionRepository profiles)
    {
        _profiles = profiles;
    }

    [Function("Admin_GetAdviserProjectionById")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/advisers/projection/{adviserId}")]
        HttpRequestData req,
        string adviserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adviserId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "adviserId is required.", ct, "Validation");

        var adviser = await _profiles.GetAsync(adviserId, ct);
        if (adviser is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Adviser projection not found.", ct, "NotFound");

        return await req.OkJsonAsync(new
        {
            adviserId = adviser.AdviserId,
            displayName = adviser.DisplayName,
            homePostcode = adviser.HomePostcode,
            region = adviser.Region,
            skills = adviser.Skills,
            rating = adviser.Rating,
            isActive = adviser.IsActive,
            coverageRadiusMiles = adviser.CoverageRadiusMiles,
            maxTravelTimeMinutes = adviser.MaxTravelTimeMinutes,
            lastSyncedUtc = adviser.LastSyncedUtc,
            sourceVersion = adviser.SourceVersion
        }, ct);
    }
}
