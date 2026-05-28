namespace AFH.Notification.Application.Models;

public sealed record NotificationBounceAuditResult(
    string BounceId,
    string? ProviderMessageId,
    string? RecipientEmail,
    string? ReasonCode,
    string? ReasonDetail,
    DateTime OccurredUtc,
    DateTime ReceivedUtc);
