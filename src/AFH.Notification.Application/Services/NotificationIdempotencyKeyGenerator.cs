using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed class NotificationIdempotencyKeyGenerator : INotificationIdempotencyKeyGenerator
{
    public string GenerateKey(NotificationRequested request, NotificationChannel channel, NotificationRecipient recipient)
    {
        var primaryId = GetPrimaryId(request);
        var templateVersion = request.Data.TryGetValue("TemplateVersion", out var version) ? version : "v1";
        
        var recipientAddress = channel switch
        {
            NotificationChannel.Email => recipient.Email ?? string.Empty,
            NotificationChannel.Sms => recipient.MobileNumber ?? string.Empty,
            _ => "unknown"
        };

        // Format: {SourceApp}:{NotificationType}:{PrimaryId}:{Channel}:{RecipientType}:{RecipientAddress}:{TemplateVersion}
        return $"{request.SourceSystem}:{request.Type.Name}:{primaryId}:{channel}:{recipient.RecipientType}:{recipientAddress}:{templateVersion}";
    }

    private static string GetPrimaryId(NotificationRequested request)
    {
        if (request.Data.TryGetValue("BookingId", out var bookingId) && !string.IsNullOrWhiteSpace(bookingId))
            return bookingId;
            
        if (request.Data.TryGetValue("HoldId", out var holdId) && !string.IsNullOrWhiteSpace(holdId))
            return holdId;
            
        if (request.Data.TryGetValue("TransactionId", out var txId) && !string.IsNullOrWhiteSpace(txId))
            return txId;

        return request.CorrelationId;
    }
}
