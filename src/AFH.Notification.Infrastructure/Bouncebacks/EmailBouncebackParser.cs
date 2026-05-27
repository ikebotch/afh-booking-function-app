using System.Text.Json;
using AFH.Notification.Application.Models;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Bouncebacks;

public sealed class EmailBouncebackParser
{
    private readonly ILogger<EmailBouncebackParser> _logger;

    public EmailBouncebackParser(ILogger<EmailBouncebackParser> logger)
    {
        _logger = logger;
    }

    public (NotificationBouncebackResult Result, IReadOnlyList<NotificationBounceback> Bouncebacks) Parse(string payload)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var events = JsonSerializer.Deserialize<JsonElement[]>(payload, options);
            if (events == null || events.Length == 0)
                return (new NotificationBouncebackResult(false, "Payload was empty or not an array"), Array.Empty<NotificationBounceback>());

            var bouncebacks = new List<NotificationBounceback>();

            foreach (var evt in events)
            {
                if (!evt.TryGetProperty("eventType", out var eventTypeProp) || eventTypeProp.ValueKind != JsonValueKind.String)
                    continue;
                    
                var eventType = eventTypeProp.GetString();
                
                if (eventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
                {
                    if (evt.TryGetProperty("data", out var data) && data.TryGetProperty("validationCode", out var validationCodeProp))
                    {
                        var validationCode = validationCodeProp.GetString();
                        return (new NotificationBouncebackResult(true, null, 0, validationCode), Array.Empty<NotificationBounceback>());
                    }
                }
                
                if (eventType == "Microsoft.Communication.EmailDeliveryReportReceived")
                {
                    if (!evt.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                        continue;

                    string? messageId = null;
                    if (data.TryGetProperty("messageId", out var messageIdProp) && messageIdProp.ValueKind == JsonValueKind.String)
                        messageId = messageIdProp.GetString();
                        
                    string? status = null;
                    if (data.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
                        status = statusProp.GetString();
                    
                    if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(status))
                    {
                        _logger.LogWarning("Invalid EmailDeliveryReportReceived payload: missing messageId or status.");
                        continue;
                    }

                    string? bounceReason = null;
                    if (data.TryGetProperty("deliveryStatusDetails", out var details) && details.ValueKind == JsonValueKind.Object)
                    {
                        if (details.TryGetProperty("statusMessage", out var statusMessage) && statusMessage.ValueKind == JsonValueKind.String)
                        {
                            bounceReason = statusMessage.GetString();
                        }
                    }
                    
                    var timestamp = DateTime.UtcNow;
                    if (evt.TryGetProperty("eventTime", out var eventTimeProp) && eventTimeProp.ValueKind == JsonValueKind.String)
                    {
                        if (DateTime.TryParse(eventTimeProp.GetString(), out var eventTime))
                        {
                            timestamp = eventTime.ToUniversalTime();
                        }
                    }

                    bouncebacks.Add(new NotificationBounceback(messageId, status, bounceReason, timestamp));
                }
            }

            return (new NotificationBouncebackResult(true, null, bouncebacks.Count), bouncebacks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse email bounceback payload.");
            return (new NotificationBouncebackResult(false, "Failed to parse JSON payload."), Array.Empty<NotificationBounceback>());
        }
    }
}
