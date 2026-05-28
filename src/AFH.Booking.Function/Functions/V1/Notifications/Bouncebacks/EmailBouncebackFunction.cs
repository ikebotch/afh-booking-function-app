using AFH.Notification.Application.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AFH.Booking.Function.Functions.V1.Notifications.Bouncebacks;

public sealed class EmailBouncebackFunction
{
    private readonly INotificationBouncebackProcessor _processor;
    private readonly ILogger<EmailBouncebackFunction> _logger;

    public EmailBouncebackFunction(
        INotificationBouncebackProcessor processor,
        ILogger<EmailBouncebackFunction> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    [Function("EmailBouncebackFunctionV1")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/notifications/email-bounceback")] HttpRequestData req,
        CancellationToken ct)
    {
        using var reader = new StreamReader(req.Body);
        var payload = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("Empty payload received for email bounceback.");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        try
        {
            var result = await _processor.ProcessWebhookPayloadAsync(payload, ct);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to process email bounceback webhook: {Error}", result.ErrorMessage);
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            _logger.LogInformation("Processed email bounceback webhook successfully. Events processed: {Count}", result.ProcessedCount);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            
            if (!string.IsNullOrEmpty(result.ValidationResponse))
            {
                await response.WriteAsJsonAsync(new { validationResponse = result.ValidationResponse }, ct);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing email bounceback webhook.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}
