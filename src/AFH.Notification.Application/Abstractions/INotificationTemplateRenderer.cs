using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationTemplateRenderer
{
    Task<NotificationTemplateRenderResult> RenderAsync(NotificationRequested notification, CancellationToken ct);
}
