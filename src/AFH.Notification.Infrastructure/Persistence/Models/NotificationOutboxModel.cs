namespace AFH.Notification.Infrastructure.Persistence.Models;

public sealed class NotificationOutboxModel
{
    public Guid Id { get; set; }
    public string SourceApplication { get; set; } = default!;
    public string NotificationType { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? QueueMessageId { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
