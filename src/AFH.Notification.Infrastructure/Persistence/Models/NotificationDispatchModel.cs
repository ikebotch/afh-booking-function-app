namespace AFH.Notification.Infrastructure.Persistence.Models;

public sealed class NotificationDispatchModel
{
    public string Id { get; set; } = default!;
    public Guid DispatchUid { get; set; }
    public string? BookingId { get; set; }
    public string? TransactionId { get; set; }
    public string? TransactionRef { get; set; }
    public string? LifecycleEventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? EventType { get; set; }
    public bool? SmsRequested { get; set; }
    public bool? EmailRequested { get; set; }
    public string? SmsStatus { get; set; }
    public string? EmailStatus { get; set; }
    public string? OutcomeCode { get; set; }
    public string? FailureDetails { get; set; }
    public string? RecipientType { get; set; }
    public string? RecipientPhone { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MessageSubject { get; set; }
    public string? MessageBody { get; set; }
    public Guid? NotificationOutboxId { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceReferenceType { get; set; }
    public string? SourceReferenceId { get; set; }
    public string? NotificationType { get; set; }
    public string? RecipientMobile { get; set; }
    public string? Channel { get; set; }
    public string? ProviderName { get; set; }
    public string? TemplateName { get; set; }
    public string? TemplateKey { get; set; }
    public string? TemplateVersion { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
