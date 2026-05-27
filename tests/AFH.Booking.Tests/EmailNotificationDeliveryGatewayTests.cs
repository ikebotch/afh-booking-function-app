using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Infrastructure.Delivery.Email;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class EmailNotificationDeliveryGatewayTests
{
    [Fact]
    public async Task SendAsync_ComposedProvider_ReturnsExplicitNonProductionStatus()
    {
        var gateway = CreateGateway(new EmailDeliveryOptions
        {
            Enabled = true,
            ProviderName = "Composed"
        });

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("NonProductionComposed", result.Status);
        Assert.NotNull(result.ProviderMessageId);
    }

    [Fact]
    public async Task SendAsync_ProductionProviderConfigured_ThrowsUntilAdapterIsWired()
    {
        var gateway = CreateGateway(new EmailDeliveryOptions
        {
            Enabled = true,
            ProviderName = "ProductionProvider"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendAsync(CreateRequest(), CancellationToken.None));

        Assert.Contains("no production provider adapter wired", ex.Message);
    }

    private static EmailNotificationDeliveryGateway CreateGateway(EmailDeliveryOptions options)
        => new(Options.Create(options), NullLogger<EmailNotificationDeliveryGateway>.Instance);

    private static NotificationDeliveryRequest CreateRequest()
        => new(
            "corr-1",
            NotificationChannel.Email,
            new NotificationRecipient("Client", "Jane Client", "jane@example.test"),
            "Subject",
            HtmlBody: null,
            "Body",
            new Dictionary<string, string>());
}
