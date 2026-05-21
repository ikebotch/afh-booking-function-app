using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using System.Text;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class ConfirmHoldFunction
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfirmBookingService _service;

    public ConfirmHoldFunction(IConfirmBookingService service)
        => _service = service;

    [Function("Bookings_ConfirmHold")]
    [BookingOpenApiOperation(
        "Bookings",
        "Confirm hold",
        HttpMethod = "post",
        Description = "Confirms the hold identified by the route holdId. The JSON body is optional and can include metadata such as notes.",
        RequestBodyType = typeof(ConfirmBookingRequest),
        RequestBodyRequired = false,
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

        var bodyReadResult = await TryReadOptionalBodyAsync(req, ct);
        if (!bodyReadResult.IsSuccess)
            return await req.ProblemAsync(
                HttpStatusCode.BadRequest,
                "Invalid JSON body.",
                ct,
                "InvalidJson");

        body = bodyReadResult.Value;

        var cmd = new ConfirmBookingCommand
        {
            HoldId = holdId.Trim(),
            BookingId = body?.BookingId ?? holdId.Trim(),
            Notes = body?.Notes
        };

        var result = await _service.HandleAsync(cmd, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to confirm hold.",
                ct,
                result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }

    private static async Task<(bool IsSuccess, ConfirmBookingRequest? Value)> TryReadOptionalBodyAsync(HttpRequestData req, CancellationToken ct)
    {
        if (req.Body is null || !req.Body.CanRead)
            return (true, null);

        using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(payload))
            return (true, null);

        try
        {
            return (true, JsonSerializer.Deserialize<ConfirmBookingRequest>(payload, JsonOpts));
        }
        catch (JsonException)
        {
            return (false, null);
        }
    }
}