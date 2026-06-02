using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Self-Service Bookings")]
public sealed class SelfServiceRearrangementOptionsFunction
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly IRearrangementOptionsService _service;

    public SelfServiceRearrangementOptionsFunction(
        IBookingChangeAccessService accessService,
        IRearrangementOptionsService service)
    {
        _accessService = accessService;
        _service = service;
    }

    [Function("Bookings_SelfServiceRearrangementOptions")]
    [BookingOpenApiOperation(
        "Self-Service Bookings",
        "Get rearrangement options by secure client token",
        Description = "Client-facing rearrangement options endpoint for the current existing booking id in the route. Frontends must call this self-service route for client journeys, not internal/admin rearrangement routes. Provide the opaque client access token as the `token` query value. Invalid or expired tokens return 401. A valid token for a different booking returns 403. The response includes top-level `transactionId`; nested availability payloads keep their `transactionId` for backwards compatibility. All returned slot times remain UTC.",
        RequestBodyType = typeof(RearrangementOptionsRequest),
        RequestBodyRequired = false,
        ResponseType = typeof(RearrangementOptionsResponse),
        RequestExampleJson = """
                             {
                               "preferredStartUtc": "2026-06-02T09:00:00Z",
                               "duration": 60,
                               "isRemote": true,
                               "meetingType": "Remote",
                               "limit": 5
                             }
                             """)]
    [BookingOpenApiQueryParameter("token", "string", Description = "Opaque client access token from the secure self-service link. Use this query parameter for the client self-service journey.", Example = "opaque-client-token")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/self-service/bookings/{bookingId}/rearrangement/options")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var access = await BookingChangeRequestContext.ValidateClientAsync(req, bookingId, _accessService, ct);
        if (!access.IsSuccess)
            return await req.ProblemAsync(access.StatusCode, access.ErrorMessage ?? "Unauthorized.", ct, access.ErrorCode);

        var body = await req.ReadJsonAsync<RearrangementOptionsRequest>(ct);
        var result = await _service.HandleAsync(new GetRearrangementOptionsCommand
        {
            BookingId = bookingId,
            PreferredStartUtc = body?.PreferredStartUtc,
            Duration = body?.Duration,
            IsRemote = body?.IsRemote,
            MeetingType = body?.MeetingType,
            Limit = body?.Limit,
            Cursor = body?.Cursor
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
