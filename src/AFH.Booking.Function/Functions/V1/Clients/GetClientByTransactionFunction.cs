using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Transactions;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Clients;

[BookingOpenApiTag("Clients")]
public sealed class GetClientByTransactionFunction
{
    private readonly IClientDirectory _clients;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ILogger<GetClientByTransactionFunction> _logger;

    public GetClientByTransactionFunction(
        IClientDirectory clients,
        IBookingTransactionRepository transactions,
        ILogger<GetClientByTransactionFunction> logger)
    {
        _clients = clients;
        _transactions = transactions;
        _logger = logger;
    }

    [Function("Client_GetByTransaction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/clients/{transactionId}")]
        HttpRequestData req,
        string transactionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "transactionId is required.", ct);

        var transactionRef = transactionId.Trim();
        _logger.LogInformation("Client lookup (transaction): {TransactionId}", transactionRef);

        var transaction = await _transactions.GetLatestByTransactionRefAsync(transactionRef, ct);
        if (transaction is not null && transaction.Status != BookingTransactionStatus.Open)
            return await req.ProblemAsync(
                HttpStatusCode.Conflict,
                $"Transaction reference '{transactionRef}' is already {transaction.Status}.",
                ct,
                "TransactionClosed");

        var client = await _clients.GetAsync(transactionRef, ct);

        if (client is null)
            return await req.ProblemAsync(HttpStatusCode.NotFound, "Client not found.", ct, "NotFound");

        var clientInfo = new
        {
            FirstName = Masking.MaskName(client.FirstName?.Trim() ?? string.Empty),
            LastName = Masking.MaskName(client.LastName?.Trim() ?? string.Empty),
            Email = Masking.MaskEmail(client.Email?.Trim() ?? string.Empty),
            PreferredStartUtc = client?.AppointmentDateTime,
            TransactionStatus = transaction?.Status.ToString(),
            IsTransactionClosed = false
        };

        return await req.OkJsonAsync(clientInfo, ct);
    }





}
