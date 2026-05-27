namespace AFH.Notification.Application.Models;

public sealed record NotificationDeliveryAuditRecord(
    string Id,
    Guid? NotificationOutboxId,
    string SourceApplication,
    string NotificationType,
    string? BookingId,
    string? TransactionId,
    string? TransactionRef,
    string Channel,
    string? RecipientEmail,
    string? RecipientPhone,
    string ProviderName,
    string Status,
    string? ProviderMessageId,
    string? FailureDetails,
    string? CorrelationId,
    string? TemplateName,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
