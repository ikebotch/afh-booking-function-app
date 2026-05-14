namespace AFH.Booking.Domain.Options;

public sealed class CalendarSubscriptionOptions
{
    public const string SectionName = "Calendars";

    public string BaseUrl { get; set; } = string.Empty;
    public string? FunctionKey { get; set; }
    public string? InternalToken { get; set; }
}
