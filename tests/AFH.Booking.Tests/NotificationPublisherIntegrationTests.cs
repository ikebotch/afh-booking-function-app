using System.Net;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Infrastructure.Composition;
using AFH.Booking.Infrastructure.Notifications;
using AFH.Booking.Infrastructure.Notifications.Options;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using AFH.Notification.Infrastructure.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class NotificationPublisherIntegrationTests
{
    [Fact]
    public async Task NotificationApiPublisher_PostsBookingNotificationRequest_WithFunctionCodeAndInternalToken()
    {
        HttpRequestMessage? captured = null;
        string? capturedJson = null;
        var publisher = new NotificationApiPublisher(
            new HttpClient(new StubHttpMessageHandler(request =>
            {
                captured = request;
                capturedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("{\"status\":\"Accepted\"}")
                };
            })),
            Options.Create(new NotificationApiPublisherOptions
            {
                BaseUrl = "https://notification.example",
                RequestPath = "/api/v1/notifications/requests",
                FunctionKey = " function-key ",
                InternalToken = " internal-token "
            }),
            NullLogger<NotificationApiPublisher>.Instance);

        await publisher.PublishAsync(CreateBookingNotification(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://notification.example/api/v1/notifications/requests?code=function-key", captured.RequestUri?.ToString());
        Assert.Equal("corr-123", captured.Headers.GetValues("x-correlation-id").Single());
        Assert.Equal("idem-123", captured.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("internal-token", captured.Headers.Authorization?.Parameter);

        Assert.NotNull(capturedJson);
        using var document = System.Text.Json.JsonDocument.Parse(capturedJson!);
        var recipients = document.RootElement.GetProperty("recipients").EnumerateArray().ToArray();
        Assert.Contains(recipients, x =>
            x.GetProperty("recipientType").GetString() == BookingNotificationRecipientTypes.Client
            && x.GetProperty("email").GetString() == "client@example.com");
        Assert.Contains(recipients, x =>
            x.GetProperty("recipientType").GetString() == BookingNotificationRecipientTypes.Adviser
            && x.GetProperty("email").GetString() == "adviser@example.com");
        Assert.Contains(recipients, x =>
            x.GetProperty("recipientType").GetString() == BookingNotificationRecipientTypes.ContactCentre
            && x.GetProperty("email").GetString() == "contactcentre@example.com");
    }

    [Theory]
    [InlineData(null, "function-key", "internal-token", "Booking:Notifications:Http:BaseUrl")]
    [InlineData("https://notification.example", null, "internal-token", "Booking:Notifications:Http:FunctionKey")]
    [InlineData("https://notification.example", "function-key", null, "Booking:Notifications:Http:InternalToken")]
    public async Task NotificationApiPublisher_MissingRequiredOptions_FailsClearly(
        string? baseUrl,
        string? functionKey,
        string? internalToken,
        string expectedKey)
    {
        var publisher = new NotificationApiPublisher(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."))),
            Options.Create(new NotificationApiPublisherOptions
            {
                BaseUrl = baseUrl,
                FunctionKey = functionKey,
                InternalToken = internalToken
            }),
            NullLogger<NotificationApiPublisher>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(CreateBookingNotification(), CancellationToken.None));

        Assert.Contains(expectedKey, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBookingInfrastructure_RegistersBookingNotificationPublisher()
    {
        var config = CreateConfig(
            ("Booking:Notifications:Http:BaseUrl", "https://notification.example"),
            ("Booking:Notifications:Http:FunctionKey", "function-key"),
            ("Booking:Notifications:Http:InternalToken", "internal-token"));
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBookingInfrastructure(config);

        using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IBookingNotificationPublisher>();

        Assert.IsType<NotificationApiPublisher>(publisher);
    }

    [Fact]
    public void AddBookingInfrastructure_DoesNotRequireNotificationHttpConfigUntilPublish()
    {
        var config = CreateConfig();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBookingInfrastructure(config);

        using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IBookingNotificationPublisher>();

        Assert.IsType<NotificationApiPublisher>(publisher);
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

    private static BookingNotificationRequest CreateBookingNotification()
        => new(
            new BookingNotificationType("Booking", "BookingConfirmed"),
            "corr-123",
            new BookingNotificationActor("System", "Booking", null, null, null),
            [
                new BookingNotificationRecipient(BookingNotificationRecipientTypes.Client, "Client", "client@example.com", null, null, [BookingNotificationChannel.Email]),
                new BookingNotificationRecipient(BookingNotificationRecipientTypes.Adviser, "Adviser", "adviser@example.com", null, null, [BookingNotificationChannel.Email]),
                new BookingNotificationRecipient(BookingNotificationRecipientTypes.ContactCentre, "Contact Centre", "contactcentre@example.com", null, null, [BookingNotificationChannel.Email])
            ],
            new Dictionary<string, string> { ["IdempotencyKey"] = "idem-123" });

    private static IConfiguration CreateConfig(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:BookingDb"] = "Server=localhost;Database=AFH.Booking;Trusted_Connection=True;TrustServerCertificate=True"
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
