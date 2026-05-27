namespace AFH.Notification.Infrastructure.Options;

public sealed class SmsDeliveryOptions
{
    public const string SectionName = "Notifications:Sms";

    public bool Enabled { get; set; }
    public string? ProviderName { get; set; }
}
