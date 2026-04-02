using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V2.Responses;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V2.Clients;

[BookingOpenApiTag("Clients")]
public sealed class GetClientByTransactionFunction
{
    private readonly IClientDirectory _clients;
    private readonly ILogger<GetClientByTransactionFunction> _logger;

    public GetClientByTransactionFunction(
        IClientDirectory clients,
        ILogger<GetClientByTransactionFunction> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    [Function("Client_GetByTransaction_V2")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v2/clients/{transactionId}")]
        HttpRequestData req,
        string transactionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "transactionId is required.", ct);

        _logger.LogInformation("Client lookup (transaction): {TransactionId}", transactionId);

        var client = await _clients.GetAsync(transactionId.Trim(), ct);
        if (client is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Client not found.", ct, "NotFound");

        var response = new GetClientResponse
        {
            FirstName = Masking.MaskName(client.FirstName?.Trim() ?? string.Empty),
            LastName = Masking.MaskName(client.LastName?.Trim() ?? string.Empty),
            Email = Masking.MaskEmail(client.Email?.Trim() ?? string.Empty),
            PreferredStartUtc = client.AppointmentDateTime
        };

        return await req.OkJsonAsync(response, ct);
    }
}
