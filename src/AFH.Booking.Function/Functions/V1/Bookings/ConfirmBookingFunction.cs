using System.Net;
using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class ConfirmHoldFunction
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfirmBookingHandler _handler;

    public ConfirmHoldFunction(IConfirmBookingHandler handler)
        => _handler = handler;

    [Function("Bookings_ConfirmHold")]
    [BookingOpenApiOperation(
        "Bookings",
        "Confirm hold",
        HttpMethod = "post",
        RequestBodyType = typeof(ConfirmBookingRequest),
        ResponseType = typeof(ConfirmBookingResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "v1/bookings/holds/{holdId}/confirm")]
        HttpRequestData req,
        string holdId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(holdId))
            return await req.ProblemAsync(
                HttpStatusCode.BadRequest,
                "holdId is required.",
                ct,
                "Validation");

        ConfirmBookingRequest? body = null;

        if (req.Body is not null && req.Body.CanRead)
        {
            try
            {
                body = await JsonSerializer.DeserializeAsync<ConfirmBookingRequest>(
                    req.Body, JsonOpts, ct);
            }
            catch (JsonException)
            {
                return await req.ProblemAsync(
                    HttpStatusCode.BadRequest,
                    "Invalid JSON body.",
                    ct,
                    "InvalidJson");
            }
        }

        var cmd = new ConfirmBookingCommand
        {
            HoldId = holdId.Trim(),
            BookingId = body?.BookingId ?? holdId.Trim(),
            Notes = body?.Notes
        };

        var result = await _handler.HandleAsync(cmd, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to confirm hold.",
                ct,
                result.ErrorCode);

        return await req.OkJsonAsync(result.Value, ct);
    }
}
