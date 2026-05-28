using AFH.Booking.Function.Http;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Notifications.Inbound;

[BookingOpenApiTag("Notifications")]
public sealed class SubmitNotificationRequestFunction
{
    private readonly INotificationRequestIngestionService _ingestionService;

    public SubmitNotificationRequestFunction(INotificationRequestIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [Function("Notifications_RequestHttpV1")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/requests")]
        HttpRequestData req,
        CancellationToken ct)
    {
        NotificationRequested? request;
        try
        {
            request = await req.ReadJsonAsync<NotificationRequested>(ct);
        }
        catch (JsonException)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body must be valid NotificationRequested JSON.", ct, "Validation");
        }

        if (request is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Request body is required.", ct, "Validation");

        try
        {
            var result = await _ingestionService.AcceptAsync(request, ct);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new NotificationRequestAcceptedResponse(
                result.NotificationRequestId,
                result.Status,
                result.CorrelationId), cancellationToken: ct);
            return response;
        }
        catch (NotificationRequestValidationException ex)
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, ex.Message, ct, "Validation");
        }
    }

    public sealed record NotificationRequestAcceptedResponse(
        Guid NotificationRequestId,
        string Status,
        string CorrelationId);
}
