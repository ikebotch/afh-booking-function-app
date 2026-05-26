namespace AFH.Booking.Domain.Options;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public bool SmsEnabled { get; set; }
    public string? SmsBaseUrl { get; set; }
    public string? SmsApiKey { get; set; }
    public string? SmsSenderId { get; set; }

    public bool EmailEnabled { get; set; }
    public string? EmailProviderName { get; set; }

    public string? ClientPortalBaseUrl { get; set; }
}
