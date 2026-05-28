namespace AFH.Notification.Infrastructure.Persistence.Models;

public sealed class NotificationTemplateModel
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = default!;
    public string TemplateVersion { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
