namespace AFH.Notification.Application.Models;

public sealed record NotificationDeliveryAuditRecord(
    string Id,
    Guid? NotificationOutboxId,
    string SourceApplication,
    string? SourceReferenceType,
    string? SourceReferenceId,
    string NotificationType,
    string Channel,
    string? RecipientType,
    string? RecipientEmail,
    string? RecipientMobile,
    string ProviderName,
    string Status,
    string? ProviderMessageId,
    string? FailureDetails,
    string? CorrelationId,
    string? TemplateKey,
    string? TemplateVersion,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string? MessageSubject = null,
    string? MessageBody = null);
