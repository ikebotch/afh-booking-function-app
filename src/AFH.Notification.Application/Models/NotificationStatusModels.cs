namespace AFH.Notification.Application.Models;

public sealed record NotificationRequestQuery(
    string? SourceApplication,
    string? SourceReferenceType,
    string? SourceReferenceId,
    string? NotificationType,
    NotificationDispatchStatus? Status,
    DateTime? FromUtc,
    DateTime? ToUtc);

public sealed record NotificationRequestStatus(
    Guid Id,
    string SourceApplication,
    string NotificationType,
    string IdempotencyKey,
    NotificationDispatchStatus Status,
    int AttemptCount,
    string? LastError,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyList<NotificationDispatchSummary> Dispatches);

public sealed record NotificationRequestSummary(
    Guid Id,
    string SourceApplication,
    string NotificationType,
    NotificationDispatchStatus Status,
    int AttemptCount,
    string? LastError,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record NotificationDispatchSummary(
    string Id,
    Guid DispatchUid,
    Guid? NotificationOutboxId,
    string? SourceApplication,
    string? SourceReferenceType,
    string? SourceReferenceId,
    string? NotificationType,
    string? CorrelationId,
    string? RecipientType,
    string? RecipientEmail,
    string? RecipientMobile,
    string? Channel,
    string? ProviderName,
    string? ProviderMessageId,
    string? TemplateKey,
    string? TemplateVersion,
    string? Status,
    string? FailureDetails,
    Guid? MessageLogId,
    DateTime CreatedUtc,
    DateTime? UpdatedUtc,
    DateTime? CompletedUtc);

public sealed record NotificationMessageLogDetail(
    Guid Id,
    Guid NotificationDispatchId,
    Guid? NotificationOutboxId,
    string? SourceApplication,
    string? NotificationType,
    string? CorrelationId,
    string? RecipientType,
    string? RecipientEmail,
    string? RecipientMobile,
    string Channel,
    string TemplateKey,
    string TemplateVersion,
    string? Subject,
    string Body,
    string ContentType,
    string? RenderDataJson,
    string? BodyHash,
    DateTime CreatedUtc);
