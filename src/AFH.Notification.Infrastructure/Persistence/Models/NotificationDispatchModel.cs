namespace AFH.Notification.Infrastructure.Persistence.Models;

public sealed class NotificationDispatchModel
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string? TransactionId { get; set; }
    public string? TransactionRef { get; set; }
    public string? LifecycleEventId { get; set; }
    public string? CorrelationId { get; set; }
    public string EventType { get; set; } = default!;
    public bool SmsRequested { get; set; }
    public bool EmailRequested { get; set; }
    public string SmsStatus { get; set; } = default!;
    public string EmailStatus { get; set; } = default!;
    public string OutcomeCode { get; set; } = "Pending";
    public string? FailureDetails { get; set; }
    public string? RecipientType { get; set; }
    public string? RecipientPhone { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MessageSubject { get; set; }
    public string? MessageBody { get; set; }
    public Guid? NotificationOutboxId { get; set; }
    public string? SourceApplication { get; set; }
    public string? NotificationType { get; set; }
    public string? Channel { get; set; }
    public string? ProviderName { get; set; }
    public string? TemplateName { get; set; }
    public string? TemplateKey { get; set; }
    public string? TemplateVersion { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
