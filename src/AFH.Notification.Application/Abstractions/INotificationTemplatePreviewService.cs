using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationTemplatePreviewService
{
    Task<NotificationTemplatePreviewResult> PreviewAsync(NotificationTemplatePreviewRequest request, CancellationToken ct);
}
