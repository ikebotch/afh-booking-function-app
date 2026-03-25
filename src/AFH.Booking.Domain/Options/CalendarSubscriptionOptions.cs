namespace AFH.Booking.Domain.Options;

public sealed class CalendarSubscriptionOptions
{
    public const string SectionName = "Calendars";

    public string BaseUrl { get; set; } = string.Empty;
    public string? InternalToken { get; set; }
    public string? NotificationsUrl { get; init; }


    public string? ClientState { get; init; }
    public bool RequireClientState { get; init; } = true;
    public int ExpirationMinutes { get; init; } = 48 * 60;
    public string Resource { get; init; } = "/users/{userId}/events";
}
