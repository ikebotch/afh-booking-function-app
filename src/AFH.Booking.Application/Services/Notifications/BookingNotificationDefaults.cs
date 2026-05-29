using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Services.Notifications;

public static class BookingNotificationDefaults
{
    public const string SourceApplication = "Booking";

    public static BookingNotificationPolicy CreatePolicy(string sourceApplication, BookingNotificationType notificationType)
    {
        var typeName = notificationType.Name;
        var eventEnabled = !string.Equals(typeName, BookingNotificationTypes.BookingHoldCreated.Name, StringComparison.Ordinal);

        return new BookingNotificationPolicy(
            sourceApplication,
            typeName,
            eventEnabled,
            CreateChannels(typeName),
            CreateRecipients(typeName));
    }

    public static IReadOnlyList<BookingNotificationChannelPolicy> CreateChannels(string notificationType)
    {
        var emailKey = notificationType switch
        {
            "BookingConfirmed" => "booking-confirmed",
            "BookingRescheduled" => "booking-rescheduled",
            "BookingCancelled" => "booking-cancelled",
            "BookingHoldCreated" => "booking-hold",
            _ => ToKebabCase(notificationType)
        };

        var smsKey = notificationType switch
        {
            "BookingConfirmed" => "booking-confirmed-sms",
            "BookingRescheduled" => "booking-rescheduled-sms",
            "BookingCancelled" => "booking-cancelled-sms",
            "BookingHoldCreated" => "booking-hold-sms",
            _ => $"{ToKebabCase(notificationType)}-sms"
        };

        var emailEnabled = !string.Equals(notificationType, "BookingHoldCreated", StringComparison.Ordinal);

        return
        [
            new BookingNotificationChannelPolicy(BookingNotificationChannel.Email, emailEnabled, emailKey, "v1"),
            new BookingNotificationChannelPolicy(BookingNotificationChannel.Sms, false, smsKey, "v1")
        ];
    }

    public static IReadOnlyList<BookingNotificationRecipientPolicy> CreateRecipients(string notificationType)
    {
        var enabled = !string.Equals(notificationType, "BookingHoldCreated", StringComparison.Ordinal);
        return
        [
            new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Client, enabled),
            new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Adviser, enabled),
            new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.ContactCentre, enabled)
        ];
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "notification";

        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
                chars.Add('-');
            chars.Add(char.ToLowerInvariant(c));
        }

        return new string([.. chars]);
    }
}
