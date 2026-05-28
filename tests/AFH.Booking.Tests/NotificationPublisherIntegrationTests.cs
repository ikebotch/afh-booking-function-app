using System.Net;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using AFH.Notification.Infrastructure.Composition;
using AFH.Notification.Infrastructure.Integration;
using AFH.Notification.Contract.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class NotificationPublisherIntegrationTests
{
    [Fact]
    public void AddNotificationInfrastructure_InvalidTransport_FailsFast()
    {
        var config = CreateConfig(("Notifications:Integration:Transport", "CarrierPigeon"));
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddNotificationInfrastructure(config));

        Assert.Contains("Transport must be Http, ServiceBus, or InProcess", ex.Message);
    }

    [Theory]
    [InlineData("Http", typeof(HttpNotificationPublisher))]
    [InlineData("ServiceBus", typeof(ServiceBusNotificationPublisher))]
    public void AddNotificationInfrastructure_RegistersConfiguredPublisher(string transport, Type expectedPublisherType)
    {
        var config = CreateConfig(("Notifications:Integration:Transport", transport));
        var services = new ServiceCollection();

        services.AddNotificationInfrastructure(config);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INotificationPublisher) &&
            (descriptor.ImplementationType == expectedPublisherType ||
             descriptor.ImplementationFactory is not null && expectedPublisherType == typeof(HttpNotificationPublisher)));
    }

    [Fact]
    public async Task HttpNotificationPublisher_PostsNotificationRequested_WithCorrelationAndIdempotencyHeaders()
    {
        HttpRequestMessage? captured = null;
        var publisher = new HttpNotificationPublisher(
            new HttpClient(new StubHttpMessageHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }))
            {
                BaseAddress = new Uri("https://notification.example")
            },
            Options.Create(new HttpNotificationPublisherOptions
            {
                BaseUrl = "https://notification.example",
                RequestPath = "/api/v1/notifications/requests",
                InternalToken = "internal-token"
            }));

        await publisher.PublishAsync(new NotificationRequested(
            new NotificationType("Booking", "BookingConfirmed"),
            "corr-123",
            new NotificationActor("System", "Booking", null, null, null),
            [new NotificationRecipient("Client", "Client", "client@example.com", null, null, [NotificationChannel.Email])],
            new Dictionary<string, string> { ["IdempotencyKey"] = "idem-123" }), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://notification.example/api/v1/notifications/requests", captured.RequestUri?.ToString());
        Assert.Equal("corr-123", captured.Headers.GetValues("x-correlation-id").Single());
        Assert.Equal("idem-123", captured.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("internal-token", captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public void ServiceBusNotificationPublisher_CreatesMessage_WithCorrelationAndIdempotency()
    {
        var notification = new NotificationRequested(
            new NotificationType("Booking", "BookingConfirmed"),
            "corr-123",
            new NotificationActor("System", "Booking", null, null, null),
            [new NotificationRecipient("Client", "Client", "client@example.com", null, null, [NotificationChannel.Email])],
            new Dictionary<string, string> { ["IdempotencyKey"] = "idem-123" });

        var message = ServiceBusNotificationPublisher.CreateServiceBusMessage(notification);

        Assert.Equal("application/json", message.ContentType);
        Assert.Equal("corr-123", message.CorrelationId);
        Assert.Equal("idem-123", message.MessageId);
        Assert.Contains("BookingConfirmed", message.Body.ToString(), StringComparison.Ordinal);
    }

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

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
