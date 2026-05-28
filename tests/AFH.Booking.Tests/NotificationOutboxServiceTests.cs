using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class NotificationOutboxServiceTests
{
    private readonly Mock<INotificationOutboxStore> _outboxStoreMock = new();
    private readonly Mock<INotificationQueuePublisher> _queuePublisherMock = new();
    private readonly Mock<INotificationIdempotencyKeyGenerator> _keyGeneratorMock = new();
    private readonly Mock<INotificationRecipientResolver> _recipientResolverMock = new();

    private readonly NotificationOutboxService _sut;

    public NotificationOutboxServiceTests()
    {
        _sut = new NotificationOutboxService(
            _outboxStoreMock.Object,
            _queuePublisherMock.Object,
            _keyGeneratorMock.Object,
            _recipientResolverMock.Object,
            NullLogger<NotificationOutboxService>.Instance);
    }

    [Fact]
    public async Task PublishAsync_CreatesOutboxItem_AndPublishesQueueMessage_WhenCreatedIsTrue()
    {
        var request = new NotificationRequested(
            new NotificationType("TestApp", "TestType"),
            "corr-123",
            new NotificationActor("System", "TestApp", null, null, null),
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, null) },
            new Dictionary<string, string>());

        var resolvedRoute = new NotificationRoute(
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, new[] { NotificationChannel.Email }) },
            false);

        _recipientResolverMock.Setup(x => x.ResolveAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedRoute);

        _keyGeneratorMock.Setup(x => x.GenerateKey(It.IsAny<NotificationRequested>(), NotificationChannel.Email, resolvedRoute.Recipients[0]))
            .Returns("TestApp:TestType:corr-123:Email:Client:test@test.com:v1");

        var outboxItem = new NotificationOutboxItem(
            Guid.NewGuid(), "TestApp", "TestType", "TestApp:TestType:corr-123:Email:Client:test@test.com:v1",
            "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationQueuePublishResult("azure-message-123"));

        await _sut.PublishAsync(request, CancellationToken.None);

        _outboxStoreMock.Verify(x => x.CreateOrGetAsync(It.Is<NotificationOutboxItem>(i => 
            i.SourceApplication == "TestApp" && 
            i.NotificationType == "TestType" &&
            i.IdempotencyKey == "TestApp:TestType:corr-123:Email:Client:test@test.com:v1"), It.IsAny<CancellationToken>()), Times.Once);

        _queuePublisherMock.Verify(x => x.PublishAsync(It.Is<NotificationQueueMessage>(m => 
            m.OutboxId == outboxItem.Id), It.IsAny<CancellationToken>()), Times.Once);
        _outboxStoreMock.Verify(x => x.MarkQueuedAsync(outboxItem.Id, "azure-message-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_DoesNotEnqueue_WhenCreatedIsFalse()
    {
        var request = new NotificationRequested(
            new NotificationType("TestApp", "TestType"),
            "corr-123",
            new NotificationActor("System", "TestApp", null, null, null),
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, null) },
            new Dictionary<string, string>());

        var resolvedRoute = new NotificationRoute(
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, new[] { NotificationChannel.Email }) },
            false);

        _recipientResolverMock.Setup(x => x.ResolveAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedRoute);

        _keyGeneratorMock.Setup(x => x.GenerateKey(It.IsAny<NotificationRequested>(), NotificationChannel.Email, resolvedRoute.Recipients[0]))
            .Returns("TestApp:TestType:corr-123:Email:Client:test@test.com:v1");

        var outboxItem = new NotificationOutboxItem(
            Guid.NewGuid(), "TestApp", "TestType", "TestApp:TestType:corr-123:Email:Client:test@test.com:v1",
            "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, false));

        await _sut.PublishAsync(request, CancellationToken.None);

        _outboxStoreMock.Verify(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _queuePublisherMock.Verify(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _outboxStoreMock.Verify(x => x.MarkQueuedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_SerializesPayloadCorrectly()
    {
        var request = new NotificationRequested(
            new NotificationType("TestApp", "TestType"),
            "corr-123",
            new NotificationActor("System", "TestApp", null, null, null),
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, null) },
            new Dictionary<string, string> { { "Key", "Value" } });

        var resolvedRoute = new NotificationRoute(
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, new[] { NotificationChannel.Email }) },
            false);

        _recipientResolverMock.Setup(x => x.ResolveAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedRoute);

        NotificationOutboxItem? capturedItem = null;
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOutboxItem, CancellationToken>((item, ct) => capturedItem = item)
            .ReturnsAsync((NotificationOutboxItem item, CancellationToken ct) => new NotificationOutboxCreateResult(item, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationQueuePublishResult("azure-message-123"));

        await _sut.PublishAsync(request, CancellationToken.None);

        Assert.NotNull(capturedItem);
        var deserialized = JsonSerializer.Deserialize<NotificationRequested>(capturedItem.PayloadJson);
        Assert.NotNull(deserialized);
        Assert.Equal("corr-123", deserialized.CorrelationId);
        Assert.True(deserialized.Data.ContainsKey("Key"));
        Assert.Equal("Value", deserialized.Data["Key"]);
    }

    [Fact]
    public async Task PublishAsync_Throws_WhenQueuePublishSucceedsButMarkQueuedFails()
    {
        var request = new NotificationRequested(
            new NotificationType("TestApp", "TestType"),
            "corr-123",
            new NotificationActor("System", "TestApp", null, null, null),
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, null) },
            new Dictionary<string, string>());

        var recipient = new NotificationRecipient("Client", "John", "test@test.com", null, null, new[] { NotificationChannel.Email });
        var resolvedRoute = new NotificationRoute(new[] { recipient }, false);

        _recipientResolverMock.Setup(x => x.ResolveAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedRoute);
        _keyGeneratorMock.Setup(x => x.GenerateKey(It.IsAny<NotificationRequested>(), NotificationChannel.Email, recipient))
            .Returns("TestApp:TestType:corr-123:Email:Client:test@test.com:v1");

        var outboxItem = new NotificationOutboxItem(
            Guid.NewGuid(), "TestApp", "TestType", "TestApp:TestType:corr-123:Email:Client:test@test.com:v1",
            "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationQueuePublishResult("azure-message-123"));
        _outboxStoreMock.Setup(x => x.MarkQueuedAsync(outboxItem.Id, "azure-message-123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mark failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.PublishAsync(request, CancellationToken.None));

        Assert.Equal("mark failed", ex.Message);
        _queuePublisherMock.Verify(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _outboxStoreMock.Verify(x => x.MarkQueuedAsync(outboxItem.Id, "azure-message-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_QueuePublishFailure_LeavesPendingAndThrows()
    {
        var request = new NotificationRequested(
            new NotificationType("TestApp", "TestType"),
            "corr-123",
            new NotificationActor("System", "TestApp", null, null, null),
            new[] { new NotificationRecipient("Client", "John", "test@test.com", null, null, null) },
            new Dictionary<string, string>());
        var recipient = new NotificationRecipient("Client", "John", "test@test.com", null, null, new[] { NotificationChannel.Email });
        var resolvedRoute = new NotificationRoute(new[] { recipient }, false);
        var outboxItem = new NotificationOutboxItem(
            Guid.NewGuid(), "TestApp", "TestType", "key", "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _recipientResolverMock.Setup(x => x.ResolveAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedRoute);
        _keyGeneratorMock.Setup(x => x.GenerateKey(It.IsAny<NotificationRequested>(), NotificationChannel.Email, recipient))
            .Returns("key");
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.PublishAsync(request, CancellationToken.None));

        Assert.Equal("queue unavailable", ex.Message);
        _outboxStoreMock.Verify(x => x.MarkQueuedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_SerializesSingleRecipientAndChannelWithChannelTemplateData()
    {
        var request = new NotificationRequested(
            new NotificationType("Booking", "BookingConfirmed"),
            "corr-123",
            new NotificationActor("System", "Booking", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string>
            {
                ["TemplateKey:Email"] = "booking-confirmed",
                ["TemplateVersion:Email"] = "v1",
                ["TemplateKey:Sms"] = "booking-confirmed-sms",
                ["TemplateVersion:Sms"] = "v1"
            });

        var recipient = new NotificationRecipient("Client", "John", "test@test.com", "07123456789", null, [NotificationChannel.Email, NotificationChannel.Sms]);
        _recipientResolverMock.Setup(x => x.ResolveAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRoute([recipient], false));
        _keyGeneratorMock.Setup(x => x.GenerateKey(It.IsAny<NotificationRequested>(), It.IsAny<NotificationChannel>(), It.IsAny<NotificationRecipient>()))
            .Returns<NotificationRequested, NotificationChannel, NotificationRecipient>((_, channel, _) => $"key-{channel}");
        var captured = new List<NotificationOutboxItem>();
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOutboxItem, CancellationToken>((item, _) => captured.Add(item))
            .ReturnsAsync((NotificationOutboxItem item, CancellationToken _) => new NotificationOutboxCreateResult(item, false));

        await _sut.PublishAsync(request, CancellationToken.None);

        Assert.Equal(2, captured.Count);
        var emailPayload = JsonSerializer.Deserialize<NotificationRequested>(captured.Single(x => x.IdempotencyKey == "key-Email").PayloadJson);
        var smsPayload = JsonSerializer.Deserialize<NotificationRequested>(captured.Single(x => x.IdempotencyKey == "key-Sms").PayloadJson);

        Assert.NotNull(emailPayload);
        Assert.Single(emailPayload!.Recipients);
        Assert.Equal([NotificationChannel.Email], emailPayload.Recipients[0].PreferredChannels);
        Assert.Equal("booking-confirmed", emailPayload.Data["TemplateKey"]);
        Assert.Equal("v1", emailPayload.Data["TemplateVersion"]);

        Assert.NotNull(smsPayload);
        Assert.Single(smsPayload!.Recipients);
        Assert.Equal([NotificationChannel.Sms], smsPayload.Recipients[0].PreferredChannels);
        Assert.Equal("booking-confirmed-sms", smsPayload.Data["TemplateKey"]);
        Assert.Equal("v1", smsPayload.Data["TemplateVersion"]);
    }
}

public class NotificationIdempotencyKeyGeneratorTests
{
    private readonly NotificationIdempotencyKeyGenerator _sut = new([new BookingNotificationIdempotencyPolicy()]);

    [Fact]
    public void GenerateKey_UsesBookingId_WhenPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("Booking", "Type"),
            "corr-123",
            new NotificationActor("Sys", "Booking", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "BookingId", "book-456" } });

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("booking:type:f279d65656fd04546df178b7497bbfe425a7a36e65afca62d5c3597a74395d90", key);
    }

    [Fact]
    public void GenerateKey_UsesHoldId_WhenBookingIdNotPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("Booking", "Type"),
            "corr-123",
            new NotificationActor("Sys", "Booking", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "HoldId", "hold-789" } });

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("booking:type:feebb9a66319b25577c95ba8b63aa2e8431db807d9a7c63439b4c955288251bb", key);
    }

    [Fact]
    public void GenerateKey_UsesTransactionId_WhenHoldIdNotPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("Booking", "Type"),
            "corr-123",
            new NotificationActor("Sys", "Booking", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "TransactionId", "tx-abc" } });

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("booking:type:5a86f1c9aa604103a17f0ca8fca4475f5f16b1d3c9a0efeab196c63032eb4b96", key);
    }

    [Fact]
    public void GenerateKey_UsesCorrelationId_WhenNoIdsPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr-123",
            new NotificationActor("Sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string>());

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("app:type:b5afd53117f926392a6091d55fc457f1753e049d8c746ef1a0caa25829285660", key);
    }

    [Fact]
    public void GenerateKey_UsesPhone_ForSms()
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr-123",
            new NotificationActor("Sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string>());

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", "1234567890", null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Sms, recipient);

        Assert.Equal("app:type:6f2a4673fdf93f9a5546607c972cb9e7e52b0e0b7187c80f000cff0d66fb5218", key);
    }

    [Fact]
    public void GenerateKey_DeduplicatesByRecipientAddressAndChannel()
    {
        var request = new NotificationRequested(
            new NotificationType("Booking", "Type"),
            "corr-123",
            new NotificationActor("Sys", "Booking", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "BookingId", "book-456" } });

        var client = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);
        var adviser = new NotificationRecipient("Adviser", "John", "john@test.com", null, null, null);

        Assert.Equal(
            _sut.GenerateKey(request, NotificationChannel.Email, client),
            _sut.GenerateKey(request, NotificationChannel.Email, adviser));
    }

    [Fact]
    public void GenerateKey_ThrowsInvalidOperationException_WhenTargetIsMissing()
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr-123",
            new NotificationActor("Sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string>());

        var recipient = new NotificationRecipient("Client", "John", null, null, null, null);

        Assert.Throws<InvalidOperationException>(() => _sut.GenerateKey(request, NotificationChannel.Email, recipient));
    }
}
