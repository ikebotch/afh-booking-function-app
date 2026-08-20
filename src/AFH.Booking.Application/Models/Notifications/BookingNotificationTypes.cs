namespace AFH.Booking.Application.Models.Notifications;

public static class BookingNotificationTypes
{
    public const string SourceApplication = "Booking";

    public const string BookingConfirmedName = "BookingConfirmed";
    public const string BookingRescheduledName = "BookingRescheduled";
    public const string BookingCancelledName = "BookingCancelled";
    public const string BookingHoldCreatedName = "BookingHoldCreated";
    public const string AdviserRequestSubmittedName = "AdviserRequestSubmitted";
    public const string AdviserRequestOutcomeName = "AdviserRequestOutcome";
    public const string CalendarEventCorrectedName = "CalendarEventCorrected";
    public const string CalendarEventCorrectionFailedName = "CalendarEventCorrectionFailed";

    public static readonly BookingNotificationType BookingConfirmed = new(SourceApplication, BookingConfirmedName);
    public static readonly BookingNotificationType BookingRescheduled = new(SourceApplication, BookingRescheduledName);
    public static readonly BookingNotificationType BookingCancelled = new(SourceApplication, BookingCancelledName);
    public static readonly BookingNotificationType BookingHoldCreated = new(SourceApplication, BookingHoldCreatedName);
    public static readonly BookingNotificationType AdviserRequestSubmitted = new(SourceApplication, AdviserRequestSubmittedName);
    public static readonly BookingNotificationType AdviserRequestOutcome = new(SourceApplication, AdviserRequestOutcomeName);
    public static readonly BookingNotificationType CalendarEventCorrected = new(SourceApplication, CalendarEventCorrectedName);
    public static readonly BookingNotificationType CalendarEventCorrectionFailed = new(SourceApplication, CalendarEventCorrectionFailedName);

    public static BookingNotificationType? TryGetByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return name.Trim() switch
        {
            BookingConfirmedName => BookingConfirmed,
            BookingRescheduledName => BookingRescheduled,
            BookingCancelledName => BookingCancelled,
            BookingHoldCreatedName => BookingHoldCreated,
            AdviserRequestSubmittedName => AdviserRequestSubmitted,
            AdviserRequestOutcomeName => AdviserRequestOutcome,
            CalendarEventCorrectedName => CalendarEventCorrected,
            CalendarEventCorrectionFailedName => CalendarEventCorrectionFailed,
            _ => null
        };
    }
}
