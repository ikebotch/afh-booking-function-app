using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Models;

public sealed record NotificationTemplateAdminItem(
    Guid Id,
    string TemplateKey,
    string TemplateVersion,
    NotificationChannel Channel,
    string Name,
    string? Description,
    string? SubjectTemplate,
    string? BodyTemplate,
    string ContentType,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record NotificationTemplateSummary(
    Guid Id,
    string TemplateKey,
    string TemplateVersion,
    NotificationChannel Channel,
    string Name,
    string? Description,
    string ContentType,
    bool IsActive,
    DateTime UpdatedUtc);

public sealed record NotificationTemplateQuery(
    string? TemplateKey,
    NotificationChannel? Channel,
    bool? IsActive);

public sealed record NotificationTemplateUpsert(
    string TemplateKey,
    string TemplateVersion,
    NotificationChannel Channel,
    string Name,
    string? Description,
    string? SubjectTemplate,
    string BodyTemplate,
    string ContentType,
    bool IsActive,
    string? Actor);

public sealed record NotificationTemplatePreviewRequest(
    string TemplateKey,
    string TemplateVersion,
    NotificationChannel Channel,
    string? SubjectTemplate,
    string? BodyTemplate,
    string ContentType,
    IReadOnlyDictionary<string, string> Data);

public sealed record NotificationTemplatePreviewResult(
    string? Subject,
    string Body,
    IReadOnlyList<string> MissingTokens,
    string UsedTemplateKey,
    string UsedTemplateVersion,
    NotificationChannel Channel);
