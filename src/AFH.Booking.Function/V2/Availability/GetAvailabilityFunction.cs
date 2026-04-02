using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V2.Responses;
using AFH.Booking.Functions.Http;
using AFH.Booking.Functions.Mapping;

namespace AFH.Booking.Functions.V2.Availability;

[BookingOpenApiTag("Availability")]
public sealed class GetAvailabilityFunction
{
    private readonly IAvailabilityHandler _handler;

    public GetAvailabilityFunction(IAvailabilityHandler handler)
    {
        _handler = handler;
    }

    [Function("Transactions_Availability_V2")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v2/transactions/{transactionId}/availability")]
        HttpRequestData req,
        string transactionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "transactionId is required.", ct);

        var body = await req.ReadJsonAsync<GetAvailabilityRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct);

        if (body.Duration <= 0)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "durationMinutes must be > 0.", ct);

        if (!AvailabilityParsing.TryParsePreferredStart(body.PreferredStartUtc, out _))
            return await req.ProblemAsync(
                HttpStatusCode.BadRequest,
                "preferredStartUtc must be either 'yyyy-MM-dd' or ISO-8601 UTC e.g. '2026-02-01T10:00:00Z'.",
                ct);

        if (body.Window is not null)
        {
            var ws = DateTime.SpecifyKind(body.Window.StartUtc, DateTimeKind.Utc);
            var we = DateTime.SpecifyKind(body.Window.EndUtc, DateTimeKind.Utc);

            if (ws == default || we == default || we <= ws)
                return await req.ProblemAsync(
                    HttpStatusCode.BadRequest,
                    "window.startUtc and window.endUtc must be valid and endUtc > startUtc.",
                    ct);
        }

        var query = body.ToQuery(transactionId);
        var result = await _handler.HandleAsync(query, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        var v1 = result.Value!;
        var response = new GetAvailabilityResponse
        {
            TransactionId = v1.TransactionId,
            Items = v1.Advisers,
            Paging = v1.Paging
        };

        var paging = HttpResponseExtensions.SinglePage(response.Items.Count);
        return await req.OkJsonAsync(response, ct, paging);
    }
}
