namespace AFH.Notification.Application.Models;

public sealed record NotificationBounceAuditRecord(
    string? ProviderMessageId,
    string? RecipientEmail,
    string? ReasonCode,
    string? ReasonDetail,
    DateTime OccurredUtc);
