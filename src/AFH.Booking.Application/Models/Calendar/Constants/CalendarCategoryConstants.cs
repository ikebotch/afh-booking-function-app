namespace AFH.Booking.Application.Models.Calendar.Constants;

public static class CalendarCategoryConstants
{
    public const string AfhBooking = "AFH Booking";
    public const string Confirmed = "Confirmed";
    public const string ShowAsRemediated = "ShowAsRemediated";
    public const string MissingEventRestored = "MissingEventRestored";

    public static readonly string[] ShowAsRemediation =
    [
        AfhBooking,
        Confirmed,
        ShowAsRemediated
    ];

    public static readonly string[] MissingEventRestore =
    [
        AfhBooking,
        Confirmed,
        MissingEventRestored
    ];
}
