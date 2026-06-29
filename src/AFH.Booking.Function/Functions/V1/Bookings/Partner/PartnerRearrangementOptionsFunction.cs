using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Models.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class PartnerRearrangementOptionsFunction
{
    private readonly IRearrangementOptionsService _service;
    private readonly ILogger<PartnerRearrangementOptionsFunction> _logger;

    public PartnerRearrangementOptionsFunction(
        IRearrangementOptionsService service,
        ILogger<PartnerRearrangementOptionsFunction>? logger = null)
    {
        _service = service;
        _logger = logger ?? NullLogger<PartnerRearrangementOptionsFunction>.Instance;
    }

    [Function("Bookings_PartnerRearrangementOptions")]
    [BookingOpenApiOperation(
        "Bookings",
        "Get partner rearrangement options",
        Description = "Returns rearrangement slot options for a partner journey. The partnerName route value is applied to the actor context as PartnerName.",
        RequestBodyType = typeof(RearrangementOptionsRequest),
        RequestBodyRequired = false,
        ResponseType = typeof(RearrangementOptionsResponse),
        RequestExampleJson = """
        {
          "preferredStartUtc": "2026-07-01T09:00:00Z",
          "limit": 5
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/partners/{partnerName}/bookings/{bookingId}/rearrangement/options")]
        HttpRequestData req,
        string partnerName,
        string bookingId,
        CancellationToken ct)
        => await HandleAsync(req, partnerName, bookingId, ct);

    private async Task<HttpResponseData> HandleAsync(
        HttpRequestData req,
        string partnerName,
        string bookingId,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<RearrangementOptionsRequest>(ct);
        Result<AFH.Booking.Application.Models.Bookings.RearrangementOptionsResponse> result;
        try
        {
            result = await _service.HandleAsync(new GetRearrangementOptionsCommand
            {
                BookingId = bookingId,
                ActorContext = BookingActorContext.Partner(
                    partnerName,
                    correlationId: BookingChangeRequestContext.GetCorrelationId(req)),
                PreferredStartUtc = body?.PreferredStartUtc,
                Duration = body?.Duration,
                IsRemote = body?.IsRemote,
                MeetingType = body?.MeetingType,
                Limit = body?.Limit,
                Cursor = body?.Cursor
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get partner rearrangement options. PartnerName={PartnerName} BookingId={BookingId}", partnerName, bookingId);
            return await req.ProblemAsync(
                HttpStatusCode.BadGateway,
                "Unable to get rearrangement options.",
                ct,
                Errors.AvailabilityLookupFailed);
        }

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
