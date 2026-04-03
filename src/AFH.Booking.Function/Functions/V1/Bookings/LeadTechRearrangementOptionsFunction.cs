using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class LeadTechRearrangementOptionsFunction
{
    private readonly IRearrangementOptionsHandler _handler;

    public LeadTechRearrangementOptionsFunction(IRearrangementOptionsHandler handler)
    {
        _handler = handler;
    }

    [Function("Bookings_LeadTechRearrangementOptions")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/leadtech/bookings/{bookingId}/rearrangement/options")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<RearrangementOptionsRequest>(ct);
        var result = await _handler.HandleAsync(new GetRearrangementOptionsCommand
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

        return await req.OkJsonAsync(result.Value!, ct);
    }
}
