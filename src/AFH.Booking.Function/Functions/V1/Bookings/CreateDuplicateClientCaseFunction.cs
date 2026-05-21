using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Clients")]
public sealed class CreateDuplicateClientCaseFunction
{
    private readonly IDuplicateClientService _duplicates;

    public CreateDuplicateClientCaseFunction(IDuplicateClientService duplicates)
    {
        _duplicates = duplicates;
    }

    [Function("Clients_CreateDuplicateCase")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/clients/duplicates/cases")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CreateDuplicateClientCaseRequest>(ct);
        if (body is null || string.IsNullOrWhiteSpace(body.PrimaryTransactionRef) || string.IsNullOrWhiteSpace(body.DuplicateTransactionRef))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "primaryTransactionRef and duplicateTransactionRef are required.", ct, "Validation");

        var response = await _duplicates.CreateCaseAsync(
            primaryTransactionRef: body.PrimaryTransactionRef,
            duplicateTransactionRef: body.DuplicateTransactionRef,
            notes: body.Notes,
            raisedBy: body.RaisedBy,
            ct: ct);

        return await req.CreatedJsonAsync(response.ToContract(), ct);
    }
}
