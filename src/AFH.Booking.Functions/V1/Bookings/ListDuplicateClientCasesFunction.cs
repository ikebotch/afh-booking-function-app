using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Functions.V1.Bookings;

public sealed class ListDuplicateClientCasesFunction
{
    private readonly IDuplicateClientService _duplicates;

    public ListDuplicateClientCasesFunction(IDuplicateClientService duplicates)
    {
        _duplicates = duplicates;
    }

    [Function("Clients_ListDuplicateCases")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/clients/duplicates/cases/pending")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var response = await _duplicates.ListPendingAsync(ct);
        return await req.OkJsonAsync(response, ct, HttpResponseExtensions.SinglePage(response.Count));
    }
}
