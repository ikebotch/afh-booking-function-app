using System.Text.Json;
using AFH.Booking.Function.Functions.V1.Notifications;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class SendNotificationQueueTriggerTests
{
    private readonly Mock<INotificationOutboxStore> _outboxStoreMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<FunctionContext> _functionContextMock = new();
    
    private readonly SendNotificationQueueTrigger _sut;

    public SendNotificationQueueTriggerTests()
    {
        _sut = new SendNotificationQueueTrigger(
            _outboxStoreMock.Object,
            _notificationServiceMock.Object,
            NullLogger<SendNotificationQueueTrigger>.Instance);

        _functionContextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_DeserializationFails_ThrowsException()
    {
        var invalidJson = "{ invalid json }";

        await Assert.ThrowsAsync<JsonException>(() => _sut.RunAsync(invalidJson, _functionContextMock.Object));
    }

    [Fact]
    public async Task RunAsync_OutboxItemMissing_ExitsSafely()
    {
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var json = JsonSerializer.Serialize(message);

        _outboxStoreMock.Setup(x => x.GetAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutboxItem?)null);

        await _sut.RunAsync(json, _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.TryMarkProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_TryMarkProcessingReturnsFalse_ExitsSafely()
    {
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var json = JsonSerializer.Serialize(message);

        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.GetAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.RunAsync(json, _functionContextMock.Object);

        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_PayloadDeserializationFails_MarksDeadLettered()
    {
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var json = JsonSerializer.Serialize(message);

        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", "{ invalid payload }", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.GetAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.RunAsync(json, _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.MarkDeadLetteredAsync(message.NotificationOutboxId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_Success_DispatchesAndMarksSent()
    {
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var json = JsonSerializer.Serialize(message);

        var request = new NotificationRequested(new NotificationType("App", "Type"), "corr", new NotificationActor("sys", "App", null, null, null), Array.Empty<NotificationRecipient>(), new Dictionary<string, string>());
        var requestJson = JsonSerializer.Serialize(request);

        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", requestJson, NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.GetAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.RunAsync(json, _functionContextMock.Object);

        _notificationServiceMock.Verify(x => x.PublishAsync(It.Is<NotificationRequested>(r => r.CorrelationId == "corr"), It.IsAny<CancellationToken>()), Times.Once);
        _outboxStoreMock.Verify(x => x.MarkSentAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DispatcherThrows_MarksFailedAndRethrows()
    {
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var json = JsonSerializer.Serialize(message);

        var request = new NotificationRequested(new NotificationType("App", "Type"), "corr", new NotificationActor("sys", "App", null, null, null), Array.Empty<NotificationRecipient>(), new Dictionary<string, string>());
        var requestJson = JsonSerializer.Serialize(request);

        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", requestJson, NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.GetAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _notificationServiceMock.Setup(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dispatcher failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RunAsync(json, _functionContextMock.Object));

        _outboxStoreMock.Verify(x => x.MarkFailedAsync(message.NotificationOutboxId, "Dispatcher failure", It.IsAny<CancellationToken>()), Times.Once);
    }
}
