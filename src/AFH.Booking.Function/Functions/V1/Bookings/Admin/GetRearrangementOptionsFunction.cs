using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class GetRearrangementOptionsFunction
{
    private readonly IRearrangementOptionsService _service;
    private readonly IBookingDetailsService _details;

    public GetRearrangementOptionsFunction(
        IRearrangementOptionsService service,
        IBookingDetailsService details)
    {
        _service = service;
        _details = details;
    }

    [Function("Bookings_GetRearrangementOptions")]
    [BookingOpenApiOperation(
        "Bookings",
        "Get rearrangement options",
        Description = "Internal/admin rearrangement options endpoint for the current existing booking. Returns replacement slot options and the availability transactionId used for option context.",
        RequestBodyType = typeof(RearrangementOptionsRequest),
        ResponseType = typeof(RearrangementOptionsResponse),
        RequestExampleJson = """
        {
          "preferredStartUtc": "2026-06-20T09:00:00Z",
          "duration": 60,
          "isRemote": true,
          "meetingType": "AnnualReview",
          "limit": 5
        }
        """)]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/bookings/{bookingId}/rearrangement/options")]
        HttpRequestData req,
        FunctionContext context,
        string bookingId,
        CancellationToken ct)
    {
        var authResult = await BookingFunctionActorContext.BuildManagerOrAdminAsync(
            req,
            context,
            BookingPermissionNames.RearrangementOptionsRead,
            ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        var details = await _details.HandleAsync(new GetBookingDetailsQuery { BookingId = bookingId.Trim() }, ct);
        if (!details.IsSuccess)
            return await req.ProblemAsync(
                details.StatusCode,
                details.ErrorMessage ?? "Request failed.",
                ct,
                details.ErrorCode);

        var user = context.GetDomainUserContext()!;
        var forbidden = await BookingFunctionActorContext.EnsureCanAccessBookingAsync(req, user, details.Value!, ct);
        if (forbidden is not null)
            return forbidden;

        var body = await req.ReadJsonAsync<RearrangementOptionsRequest>(ct);

        var cmd = new GetRearrangementOptionsCommand
        {
            BookingId = bookingId,
            ActorContext = authResult.ActorContext,
            PreferredStartUtc = body?.PreferredStartUtc,
            Duration = body?.Duration,
            IsRemote = body?.IsRemote,
            MeetingType = body?.MeetingType,
            Limit = body?.Limit,
            Cursor = body?.Cursor
        };

        var result = await _service.HandleAsync(cmd, ct);

        if (!result.IsSuccess)
        {
            return await req.ProblemAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Request failed.",
                ct,
                result.ErrorCode);
        }

        return await req.OkJsonAsync(result.Value!.ToContract(), ct);
    }
}
