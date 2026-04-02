using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

[BookingOpenApiTag("Clients")]
public sealed class ResolveDuplicateClientCaseFunction
{
    private readonly IDuplicateClientService _duplicates;

    public ResolveDuplicateClientCaseFunction(IDuplicateClientService duplicates)
    {
        _duplicates = duplicates;
    }

    [Function("Clients_ResolveDuplicateCase")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/clients/duplicates/cases/{caseId}/resolve")]
        HttpRequestData req,
        string caseId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "caseId is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<ResolveDuplicateClientCaseRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Resolution))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "resolution is required.", ct, "Validation");

        var response = await _duplicates.ResolveCaseAsync(
            caseId: caseId.Trim(),
            resolution: body.Resolution,
            resolvedBy: body.ResolvedBy,
            notes: body.Notes,
            ct: ct);

        if (response is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Duplicate case was not found.", ct, "NotFound");

        return await req.OkJsonAsync(response, ct);
    }
}
