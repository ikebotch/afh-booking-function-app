namespace AFH.Booking.Infrastructure.Notifications;

public sealed class NotificationEmailOptions
{
    public const string SectionName = "Notifications:Email";

    public string? ContactCentreEmailAddress { get; set; }
    public string? AdminBccRecipients { get; set; }
}
