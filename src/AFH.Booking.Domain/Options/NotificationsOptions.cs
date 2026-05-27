namespace AFH.Booking.Domain.Options;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public string? ClientPortalBaseUrl { get; set; }
}
