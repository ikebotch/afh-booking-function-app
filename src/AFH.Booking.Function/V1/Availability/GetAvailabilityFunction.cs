using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Functions.Http;
using AFH.Booking.Functions.Mapping;

namespace AFH.Booking.Functions.V1.Availability;

[BookingOpenApiTag("Availability")]
public sealed class GetAvailabilityFunction
{
    private readonly IAvailabilityHandler _handler;
    private readonly ILogger<GetAvailabilityFunction> _logger;

    public GetAvailabilityFunction(
        IAvailabilityHandler handler,
        ILogger<GetAvailabilityFunction> logger)
    {
        _handler = handler;
        _logger = logger;
    }


 


    [Function("Transactions_Availability")]
    public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/transactions/{transactionId}/availability")]
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

        // preferredStartUtc can be date-only or date-time
        if (!AvailabilityParsing.TryParsePreferredStart(body.PreferredStartUtc, out var preferred))
            return await req.ProblemAsync(HttpStatusCode.BadRequest,
                "preferredStartUtc must be either 'yyyy-MM-dd' or ISO-8601 UTC e.g. '2026-02-01T10:00:00Z'.", ct);

        // window optional, but if provided must be valid
        if (body.Window is not null)
        {
            var ws = DateTime.SpecifyKind(body.Window.StartUtc, DateTimeKind.Utc);
            var we = DateTime.SpecifyKind(body.Window.EndUtc, DateTimeKind.Utc);

            if (ws == default || we == default || we <= ws)
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "window.startUtc and window.endUtc must be valid and endUtc > startUtc.", ct);
        }


        var query = body.ToQuery(transactionId);

        var result = await _handler.HandleAsync(query, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        var payload = result.Value!;
        var paging = HttpResponseExtensions.SinglePage(payload.Advisers.Count);
        return await req.OkJsonAsync(payload, ct, paging);
    }


}
