namespace AFH.Booking.Domain.Options;

public sealed class CalendarProjectionOptions
{
    public const string SectionName = "CalendarProjection";

    public int DedupeWindowMinutes { get; set; } = 5;
    public int StaleAfterMinutes { get; set; } = 15;
}
