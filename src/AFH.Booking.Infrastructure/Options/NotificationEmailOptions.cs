namespace AFH.Booking.Infrastructure.Options
{
    public sealed class NotificationEmailOptions
    {
        public const string SectionName = "Notifications:Email";

        public string? ContactCentreEmailAddress { get; set; } = "[EMAIL_ADDRESS]";
        public string? AdminBccRecipients { get; set; } = "[EMAIL_ADDRESS]";
    }
}
