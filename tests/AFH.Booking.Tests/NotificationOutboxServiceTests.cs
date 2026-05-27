using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class NotificationOutboxServiceTests
{
    private readonly Mock<INotificationOutboxStore> _outboxStoreMock = new();
    private readonly Mock<INotificationQueuePublisher> _queuePublisherMock = new();
    private readonly Mock<INotificationIdempotencyKeyGenerator> _keyGeneratorMock = new();
    private readonly Mock<INotificationRecipientResolver> _recipientResolverMock = new();
    private readonly Mock<IContactCentreRoutingResolver> _contactCentreResolverMock = new();

    private readonly NotificationOutboxService _sut;

    public NotificationOutboxServiceTests()
    {
        _sut = new NotificationOutboxService(
            _outboxStoreMock.Object,
            _queuePublisherMock.Object,
            _keyGeneratorMock.Object,
            _recipientResolverMock.Object,
            _contactCentreResolverMock.Object);
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

        _keyGeneratorMock.Setup(x => x.GenerateKey(request, NotificationChannel.Email, resolvedRoute.Recipients[0]))
            .Returns("TestApp:TestType:corr-123:Email:Client:test@test.com:v1");

        var outboxItem = new NotificationOutboxItem(
            Guid.NewGuid(), "TestApp", "TestType", "TestApp:TestType:corr-123:Email:Client:test@test.com:v1",
            "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, true));

        await _sut.PublishAsync(request, CancellationToken.None);

        _outboxStoreMock.Verify(x => x.CreateOrGetAsync(It.Is<NotificationOutboxItem>(i => 
            i.SourceApplication == "TestApp" && 
            i.NotificationType == "TestType" &&
            i.IdempotencyKey == "TestApp:TestType:corr-123:Email:Client:test@test.com:v1"), It.IsAny<CancellationToken>()), Times.Once);

        _queuePublisherMock.Verify(x => x.PublishAsync(It.Is<NotificationQueueMessage>(m => 
            m.NotificationOutboxId == outboxItem.Id &&
            m.SourceApplication == "TestApp" &&
            m.NotificationType == "TestType"), It.IsAny<CancellationToken>()), Times.Once);
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

        _keyGeneratorMock.Setup(x => x.GenerateKey(request, NotificationChannel.Email, resolvedRoute.Recipients[0]))
            .Returns("TestApp:TestType:corr-123:Email:Client:test@test.com:v1");

        var outboxItem = new NotificationOutboxItem(
            Guid.NewGuid(), "TestApp", "TestType", "TestApp:TestType:corr-123:Email:Client:test@test.com:v1",
            "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, false));

        await _sut.PublishAsync(request, CancellationToken.None);

        _outboxStoreMock.Verify(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _queuePublisherMock.Verify(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()), Times.Never);
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

        NotificationOutboxItem capturedItem = null;
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOutboxItem, CancellationToken>((item, ct) => capturedItem = item)
            .ReturnsAsync((NotificationOutboxItem item, CancellationToken ct) => new NotificationOutboxCreateResult(item, true));

        await _sut.PublishAsync(request, CancellationToken.None);

        Assert.NotNull(capturedItem);
        var deserialized = JsonSerializer.Deserialize<NotificationRequested>(capturedItem.PayloadJson);
        Assert.NotNull(deserialized);
        Assert.Equal("corr-123", deserialized.CorrelationId);
        Assert.True(deserialized.Data.ContainsKey("Key"));
        Assert.Equal("Value", deserialized.Data["Key"]);
    }
}

public class NotificationIdempotencyKeyGeneratorTests
{
    private readonly NotificationIdempotencyKeyGenerator _sut = new();

    [Fact]
    public void GenerateKey_UsesBookingId_WhenPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr-123",
            new NotificationActor("Sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "BookingId", "book-456" } });

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("App:Type:book-456:Email:Client:john@test.com:v1", key);
    }

    [Fact]
    public void GenerateKey_UsesHoldId_WhenBookingIdNotPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr-123",
            new NotificationActor("Sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "HoldId", "hold-789" } });

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("App:Type:hold-789:Email:Client:john@test.com:v1", key);
    }

    [Fact]
    public void GenerateKey_UsesTransactionId_WhenHoldIdNotPresent()
    {
        var request = new NotificationRequested(
            new NotificationType("App", "Type"),
            "corr-123",
            new NotificationActor("Sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string> { { "TransactionId", "tx-abc" } });

        var recipient = new NotificationRecipient("Client", "John", "john@test.com", null, null, null);

        var key = _sut.GenerateKey(request, NotificationChannel.Email, recipient);

        Assert.Equal("App:Type:tx-abc:Email:Client:john@test.com:v1", key);
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

        Assert.Equal("App:Type:corr-123:Email:Client:john@test.com:v1", key);
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

        Assert.Equal("App:Type:corr-123:Sms:Client:1234567890:v1", key);
    }
}
