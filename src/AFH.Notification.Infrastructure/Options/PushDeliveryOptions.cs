namespace AFH.Notification.Infrastructure.Options;

public sealed class PushDeliveryOptions
{
    public const string SectionName = "Notifications:Push";

    public bool Enabled { get; set; }
    public string? ProviderName { get; set; }
}
