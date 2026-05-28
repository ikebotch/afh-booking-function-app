using System.Net;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Infrastructure.Composition;
using AFH.Notification.Infrastructure.Delivery.Sms;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class SmsNotificationDeliveryGatewayTests
{
    [Fact]
    public async Task SmsDisabled_SkipsSmsWithoutProviderSend()
    {
        var provider = new CapturingSmsProviderSender();
        var gateway = new SmsNotificationDeliveryGateway(
            Options.Create(new SmsDeliveryOptions { Enabled = false, ProviderName = "Twilio" }),
            [provider],
            NullLogger<SmsNotificationDeliveryGateway>.Instance);

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("ConfiguredOff", result.Status);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task ComposedSms_DoesNotSendRealSms()
    {
        var gateway = new SmsNotificationDeliveryGateway(
            Options.Create(new SmsDeliveryOptions { Enabled = true, ProviderName = "Composed" }),
            [],
            NullLogger<SmsNotificationDeliveryGateway>.Instance);

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("NonProductionComposed", result.Status);
        Assert.Equal("Composed", result.ProviderName);
        Assert.StartsWith("sms-composed-", result.ProviderMessageId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingMobile_SkipsSms()
    {
        var provider = new CapturingSmsProviderSender();
        var gateway = new SmsNotificationDeliveryGateway(
            Options.Create(new SmsDeliveryOptions { Enabled = true, ProviderName = "Twilio" }),
            [provider],
            NullLogger<SmsNotificationDeliveryGateway>.Instance);

        var result = await gateway.SendAsync(CreateRequest(mobile: null), CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public void AddNotificationInfrastructure_UnknownSmsProvider_FailsFast()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddNotificationInfrastructure(CreateConfig(
            ("Notifications:Sms:Enabled", "true"),
            ("Notifications:Sms:ProviderName", "SendGrid"))));

        Assert.Contains("ProviderName must be Composed, AzureCommunicationServices, or Twilio", ex.Message);
    }

    [Fact]
    public void AddNotificationInfrastructure_AzureCommunicationServices_SelectsSmsGateway()
    {
        var services = new ServiceCollection();

        services.AddNotificationInfrastructure(CreateConfig(
            ("Notifications:Sms:Enabled", "true"),
            ("Notifications:Sms:ProviderName", "AzureCommunicationServices"),
            ("Notifications:Sms:AzureCommunicationServices:ConnectionString", "endpoint=https://sms.example.communication.azure.com/;accesskey=AAAAAAAAAAAAAAAAAAAAAA=="),
            ("Notifications:Sms:AzureCommunicationServices:FromPhoneNumber", "+18005550100")));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INotificationDeliveryGateway) && descriptor.ImplementationType == typeof(SmsNotificationDeliveryGateway));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISmsProviderSender));
    }

    [Fact]
    public void AddNotificationInfrastructure_Twilio_SelectsSmsGateway()
    {
        var services = new ServiceCollection();

        services.AddNotificationInfrastructure(CreateConfig(
            ("Notifications:Sms:Enabled", "true"),
            ("Notifications:Sms:ProviderName", "Twilio"),
            ("Notifications:Sms:Twilio:AccountSid", "AC123"),
            ("Notifications:Sms:Twilio:AuthToken", "secret"),
            ("Notifications:Sms:Twilio:FromPhoneNumber", "+18005550100")));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INotificationDeliveryGateway) && descriptor.ImplementationType == typeof(SmsNotificationDeliveryGateway));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISmsProviderSender));
    }

    [Fact]
    public void AddNotificationInfrastructure_MissingTwilioConfig_FailsClearly()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddNotificationInfrastructure(CreateConfig(
            ("Notifications:Sms:Enabled", "true"),
            ("Notifications:Sms:ProviderName", "Twilio"))));

        Assert.Contains("AccountSid is required", ex.Message);
    }

    [Fact]
    public void AddNotificationInfrastructure_MissingAcsConfig_FailsClearly()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddNotificationInfrastructure(CreateConfig(
            ("Notifications:Sms:Enabled", "true"),
            ("Notifications:Sms:ProviderName", "AzureCommunicationServices"))));

        Assert.Contains("FromPhoneNumber is required", ex.Message);
    }

    [Fact]
    public async Task TwilioSmsSender_PostsMessageAndReturnsProviderMessageId()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var sender = new TwilioSmsSender(
            new HttpClient(new StubHttpMessageHandler(request =>
            {
                captured = request;
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"sid":"SM123"}""")
                };
            }))
            {
                BaseAddress = new Uri("https://api.twilio.com/")
            },
            Options.Create(new TwilioSmsOptions
            {
                AccountSid = "AC123",
                AuthToken = "secret",
                FromPhoneNumber = "+18005550100"
            }));

        var result = await sender.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("Sent", result.Status);
        Assert.Equal("SM123", result.ProviderMessageId);
        Assert.Equal("Twilio", result.ProviderName);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages.json", captured.RequestUri?.ToString());
        Assert.Contains("To=%2B447700900000", capturedBody, StringComparison.Ordinal);
        Assert.Contains("From=%2B18005550100", capturedBody, StringComparison.Ordinal);
        Assert.Contains("Body=SMS+body", capturedBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("+447700900000", "+447700900000")]
    [InlineData("+44 7700 900000", "+447700900000")]
    public void SmsPhoneNumber_NormalizesE164(string value, string expected)
    {
        Assert.True(SmsPhoneNumber.TryNormalize(value, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("07700900000")]
    [InlineData("not-a-number")]
    public void SmsPhoneNumber_RejectsNonE164(string value)
    {
        Assert.False(SmsPhoneNumber.TryNormalize(value, out _));
    }

    private static NotificationDeliveryRequest CreateRequest(string? mobile = "+447700900000")
        => new(
            "corr-1",
            NotificationChannel.Sms,
            new NotificationRecipient("Client", "Jane", null, mobile, null, [NotificationChannel.Sms]),
            Subject: null,
            HtmlBody: null,
            TextBody: "SMS body");

    private static IConfiguration CreateConfig(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:BookingDb"] = "Server=localhost;Database=AFH.Booking;Trusted_Connection=True;TrustServerCertificate=True",
            ["Notifications:Queue:QueueName"] = "notifications-send",
            ["Notifications:Queue:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Notifications:Integration:Transport"] = "Http"
        };

        foreach (var (key, value) in overrides)
            values[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class CapturingSmsProviderSender : ISmsProviderSender
    {
        public List<NotificationDeliveryRequest> Requests { get; } = [];

        public Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new NotificationDeliveryResult("Sent", "provider-id", "Twilio"));
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
