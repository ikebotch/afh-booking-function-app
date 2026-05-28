using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationTemplateStore
{
    Task<NotificationTemplateDefinition?> GetAsync(
        string templateKey,
        string templateVersion,
        NotificationChannel channel,
        CancellationToken ct);
}
