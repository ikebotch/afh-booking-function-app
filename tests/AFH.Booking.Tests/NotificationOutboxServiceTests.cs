using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Options;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
            _contactCentreResolverMock.Object,
            Options.Create(new NotificationOutboxDispatchOptions { DispatcherMode = NotificationOutboxDispatchOptions.AzureQueueMode }),
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

        _keyGeneratorMock.Setup(x => x.GenerateKey(request, NotificationChannel.Email, resolvedRoute.Recipients[0]))
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
            m.NotificationOutboxId == outboxItem.Id &&
            m.SourceApplication == "TestApp" &&
            m.NotificationType == "TestType"), It.IsAny<CancellationToken>()), Times.Once);
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
        _outboxStoreMock.Verify(x => x.MarkQueuedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_SqlDispatcherMode_DoesNotPublishQueueMessage()
    {
        var sut = new NotificationOutboxService(
            _outboxStoreMock.Object,
            _queuePublisherMock.Object,
            _keyGeneratorMock.Object,
            _recipientResolverMock.Object,
            _contactCentreResolverMock.Object,
            Options.Create(new NotificationOutboxDispatchOptions { DispatcherMode = NotificationOutboxDispatchOptions.SqlMode }),
            NullLogger<NotificationOutboxService>.Instance);
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
        _keyGeneratorMock.Setup(x => x.GenerateKey(request, NotificationChannel.Email, recipient))
            .Returns("key");
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(outboxItem, true));

        await sut.PublishAsync(request, CancellationToken.None);

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
        _keyGeneratorMock.Setup(x => x.GenerateKey(request, NotificationChannel.Email, recipient))
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

        Assert.Equal("booking:type:165b5ea005f94d759252f1331f42b2f2d47ad75195b45d7d8cc2ab663c43ecd9", key);
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

        Assert.Equal("booking:type:e91c1226e835565db08694d5195f5c428d4f37d866969100380d357d60a9f4bd", key);
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

        Assert.Equal("booking:type:be3f91bdfdca1b7147254f855d7bff98a58031b59f8c3d977f27d3c2a8afb776", key);
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

        Assert.Equal("app:type:23810f4961f31e45f351a0ca3269a389cc39242b307a2f7f35b6266be76d6dc5", key);
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

        Assert.Equal("app:type:5d63d0555ead69e366e3e52be466df87d840cb4fec61db3f34167b619232c3f9", key);
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
