using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Admin;

public sealed class GetAdviserCoverageFunction
{
    private readonly IAdminCoverageService _coverageService;

    public GetAdviserCoverageFunction(IAdminCoverageService coverageService)
    {
        _coverageService = coverageService;
    }

    [Function("Admin_GetAdviserCoverage")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/admin/adviser-coverage")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var data = await _coverageService.GetCoverageAsync(ct);
        if (data is null)
            return await req.ProblemAsync(HttpStatusCode.BadGateway, "Coverage source is unavailable.", ct, "CoverageUnavailable");

        return await req.OkJsonAsync(data, ct);
    }
}
