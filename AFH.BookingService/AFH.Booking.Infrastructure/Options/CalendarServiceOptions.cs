namespace AFH.Booking.Infrastructure.Options;

public sealed class CalendarServiceOptions
{
    public const string SectionName = "CalendarService";

    public string BaseUrl { get; set; } = string.Empty;
    public string FunctionKey { get; set; } = string.Empty;
}
