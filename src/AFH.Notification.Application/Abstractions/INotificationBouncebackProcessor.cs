using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationBouncebackProcessor
{
    Task<NotificationBouncebackResult> ProcessWebhookPayloadAsync(
        string payload,
        CancellationToken ct);
}
