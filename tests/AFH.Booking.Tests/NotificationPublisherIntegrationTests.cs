using System.Net;
using AFH.Notification.Application.Composition;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using AFH.Notification.Infrastructure.Composition;
using AFH.Notification.Infrastructure.Integration;
using AFH.Notification.Contract.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public void NotificationPublisher_ResolvesHttpPublisher_WhenTransportIsHttp()
    {
        var config = CreateConfig(
            ("Notifications:Integration:Transport", "Http"),
            ("Notifications:Integration:Http:BaseUrl", "https://notification.example"));
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddNotificationApplication();
        services.AddNotificationInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<INotificationPublisher>();

        Assert.IsType<HttpNotificationPublisher>(publisher);
    }

    [Fact]
    public void NotificationPublisher_ResolvesHttpPublisher_WhenNotificationApplicationIsRegisteredAfterInfrastructure()
    {
        var config = CreateConfig(
            ("Notifications:Integration:Transport", "Http"),
            ("Notifications:Integration:Http:BaseUrl", "https://notification.example"));
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddNotificationInfrastructure(config);
        services.AddNotificationApplication();
        using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<INotificationPublisher>();

        Assert.IsType<HttpNotificationPublisher>(publisher);
    }

    [Fact]
    public void AddNotificationInfrastructure_UsesSharedInternalApiToken_WhenNotificationTokenIsNotConfigured()
    {
        var config = CreateConfig(
            ("Notifications:Integration:Http:BaseUrl", "https://notification.example"),
            ("InternalApiAuth:Token", "shared-internal-token"));
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddNotificationInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<HttpNotificationPublisherOptions>>().Value;

        Assert.Equal("shared-internal-token", options.InternalToken);
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
                FunctionKey = "notification-function-key",
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
        Assert.Equal("notification-function-key", captured.Headers.GetValues("x-functions-key").Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("internal-token", captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task HttpNotificationPublisher_UsesNotificationInternalToken_WhenConfigured()
    {
        HttpRequestMessage? captured = null;
        var publisher = CreateHttpPublisher(
            request => captured = request,
            notificationInternalToken: "notification-token");

        await publisher.PublishAsync(CreateNotification(), CancellationToken.None);

        Assert.Equal("Bearer", captured?.Headers.Authorization?.Scheme);
        Assert.Equal("notification-token", captured?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task HttpNotificationPublisher_UsesFunctionKeyHeader_WhenConfigured()
    {
        HttpRequestMessage? captured = null;
        var publisher = CreateHttpPublisher(
            request => captured = request,
            notificationInternalToken: "notification-token",
            notificationFunctionKey: "notification-function-key");

        await publisher.PublishAsync(CreateNotification(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("notification-function-key", functionKeyValues!.Single());
        Assert.DoesNotContain("code=", captured!.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpNotificationPublisher_MissingInternalToken_FailsClearly()
    {
        var publisher = CreateHttpPublisher(
            _ => throw new InvalidOperationException("HTTP should not be called."),
            notificationInternalToken: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(CreateNotification(), CancellationToken.None));

        Assert.Equal(
            "InternalApiAuth:Token is required for HTTP notification publishing unless Notifications:Integration:Http:InternalToken is configured.",
            ex.Message);
    }

    [Fact]
    public async Task HttpNotificationPublisher_EmptyInternalToken_FailsClearly()
    {
        var publisher = CreateHttpPublisher(
            _ => throw new InvalidOperationException("HTTP should not be called."),
            notificationInternalToken: "  ");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(CreateNotification(), CancellationToken.None));

        Assert.Equal(
            "InternalApiAuth:Token is required for HTTP notification publishing unless Notifications:Integration:Http:InternalToken is configured.",
            ex.Message);
    }

    [Fact]
    public async Task HttpNotificationPublisher_DoesNotLogTokenValues()
    {
        HttpRequestMessage? captured = null;
        var logger = new CapturingLogger<HttpNotificationPublisher>();
        var publisher = CreateHttpPublisher(
            request => captured = request,
            notificationInternalToken: "notification-secret-token",
            logger: logger);

        await publisher.PublishAsync(CreateNotification(), CancellationToken.None);

        Assert.Equal("notification-secret-token", captured?.Headers.Authorization?.Parameter);
        var logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain("notification-secret-token", logs, StringComparison.Ordinal);
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

    private static HttpNotificationPublisher CreateHttpPublisher(
        Action<HttpRequestMessage> capture,
        string? notificationInternalToken,
        string? notificationFunctionKey = null,
        ILogger<HttpNotificationPublisher>? logger = null)
    {
        return new HttpNotificationPublisher(
            new HttpClient(new StubHttpMessageHandler(request =>
            {
                capture(request);
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }))
            {
                BaseAddress = new Uri("https://notification.example")
            },
            Options.Create(new HttpNotificationPublisherOptions
            {
                BaseUrl = "https://notification.example",
                RequestPath = "/api/v1/notifications/requests",
                FunctionKey = notificationFunctionKey,
                InternalToken = notificationInternalToken
            }),
            logger);
    }

    private static NotificationRequested CreateNotification()
        => new(
            new NotificationType("Booking", "BookingConfirmed"),
            "corr-123",
            new NotificationActor("System", "Booking", null, null, null),
            [new NotificationRecipient("Client", "Client", "client@example.com", null, null, [NotificationChannel.Email])],
            new Dictionary<string, string> { ["IdempotencyKey"] = "idem-123" });

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
