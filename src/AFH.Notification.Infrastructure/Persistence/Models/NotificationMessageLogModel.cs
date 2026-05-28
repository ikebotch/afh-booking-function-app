namespace AFH.Notification.Infrastructure.Persistence.Models;

public sealed class NotificationMessageLogModel
{
    public Guid Id { get; set; }
    public Guid NotificationDispatchId { get; set; }
    public Guid? NotificationOutboxId { get; set; }
    public string? SourceApplication { get; set; }
    public string? NotificationType { get; set; }
    public string? CorrelationId { get; set; }
    public string? RecipientType { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientMobile { get; set; }
    public string Channel { get; set; } = default!;
    public string TemplateKey { get; set; } = default!;
    public string TemplateVersion { get; set; } = default!;
    public Guid? TemplateContentId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string? RenderDataJson { get; set; }
    public string? BodyHash { get; set; }
    public DateTime CreatedUtc { get; set; }
}
