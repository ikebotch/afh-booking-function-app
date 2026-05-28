using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Models;

public sealed record NotificationTemplateDefinition(
    string TemplateKey,
    string TemplateVersion,
    NotificationChannel Channel,
    string Name,
    string? Description,
    string? SubjectTemplate,
    string BodyTemplate,
    string ContentType,
    bool IsActive);
