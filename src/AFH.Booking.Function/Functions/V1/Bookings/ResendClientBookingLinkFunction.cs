using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using AppCreateBookingAccessLinkRequest = AFH.Booking.Application.Abstractions.Bookings.CreateBookingAccessLinkRequest;
using ContractBookingAccessLinkResponse = AFH.Booking.Contracts.V1.Responses.BookingAccessLinkResponse;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class ResendClientBookingLinkFunction
{
    private readonly IBookingChangeAccessService _accessService;
    private readonly IUnitOfWork _uow;

    public ResendClientBookingLinkFunction(
        IBookingChangeAccessService accessService,
        IUnitOfWork uow)
    {
        _accessService = accessService;
        _uow = uow;
    }

    [Function("Bookings_ResendClientLink")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bookings/{bookingId}/client-link/resend")]
        HttpRequestData req,
        string bookingId,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CreateBookingAccessLinkRequest>(ct) ?? new CreateBookingAccessLinkRequest();
        var result = await _accessService.ResendClientLinkAsync(new AppCreateBookingAccessLinkRequest
        {
            BookingId = bookingId,
            ActorId = body.ActorId,
            CreatedBy = body.CreatedBy,
            ExpiryHours = body.ExpiryHours
        }, ct);

        if (!result.IsSuccess)
            return await req.ProblemAsync(result.StatusCode, result.ErrorMessage ?? "Request failed.", ct, result.ErrorCode);

        await _uow.SaveChangesAsync(ct);
        return await req.OkJsonAsync(ToContract(result.Value!), ct);
    }

    private static ContractBookingAccessLinkResponse ToContract(BookingAccessLinkResponse value) => new()
    {
        LinkId = value.LinkId,
        BookingId = value.BookingId,
        AccessToken = value.AccessToken,
        AccessUrl = value.AccessUrl,
        ExpiresUtc = value.ExpiresUtc,
        TransactionRef = value.TransactionRef
    };
}
