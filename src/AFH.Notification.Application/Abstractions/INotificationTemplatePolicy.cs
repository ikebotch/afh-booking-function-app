using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationTemplatePolicy
{
    bool CanHandle(NotificationType notificationType);

    string GetTemplateName(NotificationType notificationType);
}
