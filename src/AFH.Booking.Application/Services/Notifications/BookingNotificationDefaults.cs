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
            BookingNotificationTypes.BookingConfirmedName => "booking-confirmed",
            BookingNotificationTypes.BookingRescheduledName => "booking-rescheduled",
            BookingNotificationTypes.BookingCancelledName => "booking-cancelled",
            BookingNotificationTypes.BookingHoldCreatedName => "booking-hold",
            BookingNotificationTypes.CalendarEventCorrectedName => "calendar-event-corrected",
            BookingNotificationTypes.CalendarEventCorrectionFailedName => "calendar-event-correction-failed",
            _ => ToKebabCase(notificationType)
        };

        var smsKey = notificationType switch
        {
            BookingNotificationTypes.BookingConfirmedName => "booking-confirmed-sms",
            BookingNotificationTypes.BookingRescheduledName => "booking-rescheduled-sms",
            BookingNotificationTypes.BookingCancelledName => "booking-cancelled-sms",
            BookingNotificationTypes.BookingHoldCreatedName => "booking-hold-sms",
            BookingNotificationTypes.CalendarEventCorrectedName => "calendar-event-corrected-sms",
            BookingNotificationTypes.CalendarEventCorrectionFailedName => "calendar-event-correction-failed-sms",
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
        if (notificationType is BookingNotificationTypes.CalendarEventCorrectedName or BookingNotificationTypes.CalendarEventCorrectionFailedName)
        {
            return
            [
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Client, false),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Adviser, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.Manager, true),
                new BookingNotificationRecipientPolicy(BookingNotificationRecipientTypes.ContactCentre, notificationType == BookingNotificationTypes.CalendarEventCorrectionFailedName)
            ];
        }

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
