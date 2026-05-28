namespace AFH.Notification.Infrastructure.Options;

public sealed class SmsDeliveryOptions
{
    public const string SectionName = "Notifications:Sms";

    public bool Enabled { get; set; }
    public string? ProviderName { get; set; } = "Composed";
    public string? DefaultSender { get; set; }
}

public sealed class AzureCommunicationSmsOptions
{
    public const string SectionName = "Notifications:Sms:AzureCommunicationServices";

    public string? ConnectionString { get; set; }
    public string? Endpoint { get; set; }
    public bool UseManagedIdentity { get; set; }
    public string? FromPhoneNumber { get; set; }
    public bool DeliveryReportEnabled { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FromPhoneNumber))
            throw new InvalidOperationException($"{SectionName}:FromPhoneNumber is required when ACS SMS is selected.");

        if (!UseManagedIdentity && string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException($"{SectionName}:ConnectionString is required when ACS SMS is selected without managed identity.");

        if (UseManagedIdentity && string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException($"{SectionName}:Endpoint is required when ACS SMS managed identity is selected.");
    }
}

public sealed class TwilioSmsOptions
{
    public const string SectionName = "Notifications:Sms:Twilio";

    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? FromPhoneNumber { get; set; }
    public string? MessagingServiceSid { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AccountSid))
            throw new InvalidOperationException($"{SectionName}:AccountSid is required when Twilio SMS is selected.");
        if (string.IsNullOrWhiteSpace(AuthToken))
            throw new InvalidOperationException($"{SectionName}:AuthToken is required when Twilio SMS is selected.");
        if (string.IsNullOrWhiteSpace(FromPhoneNumber) && string.IsNullOrWhiteSpace(MessagingServiceSid))
            throw new InvalidOperationException($"{SectionName}:FromPhoneNumber or MessagingServiceSid is required when Twilio SMS is selected.");
    }
}
