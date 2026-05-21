using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Notifications")]
public sealed class RecordEmailBounceFunction
{
    private readonly IEmailBounceService _emailBounceService;

    public RecordEmailBounceFunction(IEmailBounceService emailBounceService)
    {
        _emailBounceService = emailBounceService;
    }

    [Function("Bookings_RecordEmailBounce")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/email/bounces")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<EmailBounceWebhookRequest>(ct);
        if (body is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        var response = await _emailBounceService.RecordBounceAsync(body.ToApplication(), ct);
        return await req.CreatedJsonAsync(response.ToContract(), ct);
    }
}
