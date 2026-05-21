using System.Net;
using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class RecordNoShowFunction
{
    private readonly INoShowBookingService _service;
    private readonly ILogger<RecordNoShowFunction> _logger;

    public RecordNoShowFunction(
        INoShowBookingService service,
        ILogger<RecordNoShowFunction> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("Bookings_RecordNoShow")]
    [BookingOpenApiOperation(
        "Bookings",
        "Record no show",
        RequestBodyType = typeof(RecordNoShowRequest),
        ResponseType = typeof(RecordNoShowResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/no-show")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, Errors.Validation);

            var body = await req.ReadJsonAsync<RecordNoShowRequest>(ct) ?? new RecordNoShowRequest();
            var result = await _service.HandleAsync(new RecordNoShowCommand
            {
                BookingId = bookingId.Trim(),
                RequestedBy = string.IsNullOrWhiteSpace(body.RequestedBy)
                    ? LifecycleActors.System
                    : body.RequestedBy.Trim(),
                ActorId = body.ActorId,
                ReasonCode = body.ReasonCode,
                ReasonDetail = body.ReasonDetail,
                CorrelationId = BookingChangeRequestContext.GetCorrelationId(req)
            }, ct);

            if (!result.IsSuccess)
                return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

            return await req.OkJsonAsync(result.Value!, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON body in Bookings_RecordNoShow.");
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid JSON body.", ct, "InvalidJson");
        }
    }
}