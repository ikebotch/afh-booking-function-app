namespace AFH.Booking.Domain.Options;

public sealed class LifecycleNotificationOptions
{
    public const string SectionName = "Lifecycle:Notifications";

    public bool Enabled { get; set; } = true;
    public bool RecordFailures { get; set; } = true;
}
