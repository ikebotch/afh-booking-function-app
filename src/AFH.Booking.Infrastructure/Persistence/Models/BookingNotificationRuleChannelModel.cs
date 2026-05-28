namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class BookingNotificationRuleChannelModel
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public string Channel { get; set; } = default!;
    public bool Enabled { get; set; }
    public string TemplateKey { get; set; } = default!;
    public string TemplateVersion { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public BookingNotificationRuleModel? Rule { get; set; }
}
