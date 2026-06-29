using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Models.Common;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class LeadTechRearrangementOptionsFunction
{
    private readonly IRearrangementOptionsService _service;
    private readonly ILogger<LeadTechRearrangementOptionsFunction> _logger;

    public LeadTechRearrangementOptionsFunction(
        IRearrangementOptionsService service,
        ILogger<LeadTechRearrangementOptionsFunction>? logger = null)
    {
        _service = service;
        _logger = logger ?? NullLogger<LeadTechRearrangementOptionsFunction>.Instance;
    }

    [Function("Bookings_LeadTechRearrangementOptions")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/leadtech/bookings/{bookingId}/rearrangement/options")]
        HttpRequestData req,
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
                ActorContext = BookingActorContext.LeadTech(
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
            _logger.LogError(ex, "Failed to get LeadTech rearrangement options. BookingId={BookingId}", bookingId);
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
