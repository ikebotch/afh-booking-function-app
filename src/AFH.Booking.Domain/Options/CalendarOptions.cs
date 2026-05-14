namespace AFH.Booking.Domain.Options;

public sealed class CalendarOptions
{
    public const string SectionName = "Calendar";

    /// <summary>Default timezone to use when none provided.</summary>
    public string DefaultTimezone { get; set; } = "Europe/London";

    /// <summary>Whether calendar integration is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional category prefix for events (e.g. "AFH Booking").
    /// </summary>
    public string? CategoryPrefix { get; set; } = "AFH Booking";
}
