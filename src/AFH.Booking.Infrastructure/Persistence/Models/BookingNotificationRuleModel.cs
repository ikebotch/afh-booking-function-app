namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class BookingNotificationRuleModel
{
    public Guid Id { get; set; }
    public string SourceApplication { get; set; } = default!;
    public string NotificationType { get; set; } = default!;
    public bool Enabled { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<BookingNotificationRuleChannelModel> Channels { get; set; } = [];
    public List<BookingNotificationRuleRecipientModel> Recipients { get; set; } = [];
}
