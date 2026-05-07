using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class SelfServiceRearrangementOptionsFunction
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly IRearrangementOptionsHandler _handler;

    public SelfServiceRearrangementOptionsFunction(
        IBookingChangeAccessService accessService,
        IRearrangementOptionsHandler handler)
    {
        _accessService = accessService;
        _handler = handler;
    }

    [Function("Bookings_SelfServiceRearrangementOptions")]
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
        var targetBookingId = access.Value?.CurrentBookingId ?? bookingId.Trim();
        var result = await _handler.HandleAsync(new GetRearrangementOptionsCommand
        {
            BookingId = targetBookingId,
            PreferredStartUtc = body?.PreferredStartUtc,
            Duration = body?.Duration,
            IsRemote = body?.IsRemote,
            MeetingType = body?.MeetingType,
            Limit = body?.Limit,
            Cursor = body?.Cursor
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
