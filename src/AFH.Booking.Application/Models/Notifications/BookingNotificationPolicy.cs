namespace AFH.Booking.Application.Models.Notifications;

public sealed record BookingNotificationPolicy(
    string SourceApplication,
    string NotificationType,
    bool Enabled,
    IReadOnlyList<BookingNotificationChannelPolicy> Channels,
    IReadOnlyList<BookingNotificationRecipientPolicy> Recipients)
{
    public BookingNotificationChannelPolicy? GetChannel(BookingNotificationChannel channel)
        => Channels.FirstOrDefault(x => x.Channel == channel);
}

public sealed record BookingNotificationChannelPolicy(
    BookingNotificationChannel Channel,
    bool Enabled,
    string TemplateKey,
    string TemplateVersion);

public sealed record BookingNotificationRecipientPolicy(
    string RecipientType,
    bool Enabled);
