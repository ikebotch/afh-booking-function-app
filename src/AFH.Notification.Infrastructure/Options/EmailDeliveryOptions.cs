namespace AFH.Notification.Infrastructure.Options;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "Notifications:Email";

    public bool Enabled { get; set; } = true;
    public string? ProviderName { get; set; }
    public string? ContactCentreEmailAddress { get; set; }
}
