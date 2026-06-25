namespace AFH.Notification.Infrastructure.Persistence.Models;

public sealed class NotificationSettingModel
{
    public string Key { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Value { get; set; } = default!;
    public bool IsSecret { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
