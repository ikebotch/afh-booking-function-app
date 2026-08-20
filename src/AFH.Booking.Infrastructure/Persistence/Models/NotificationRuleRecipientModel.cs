namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class NotificationRuleRecipientModel
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public string RecipientType { get; set; } = default!;
    public bool Enabled { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public NotificationRuleModel? Rule { get; set; }
}
