using System.Text.Json;
using AFH.Booking.Function.Functions.V1.Notifications;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Options;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class SendNotificationQueueTriggerTests
{
    private readonly Mock<INotificationOutboxStore> _outboxStoreMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<FunctionContext> _functionContextMock = new();

    public SendNotificationQueueTriggerTests()
    {
        _functionContextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_SqlMode_NoOps()
    {
        var sut = CreateSut(NotificationOutboxDispatchOptions.SqlMode);
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };

        await sut.RunAsync(JsonSerializer.Serialize(message), _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.TryMarkProcessingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_DeserializationFails_ThrowsException()
    {
        var sut = CreateSut(NotificationOutboxDispatchOptions.AzureQueueMode);

        await Assert.ThrowsAsync<JsonException>(() => sut.RunAsync("{ invalid json }", _functionContextMock.Object));
    }

    [Fact]
    public async Task RunAsync_TryMarkProcessingReturnsNull_ExitsSafely()
    {
        var sut = CreateSut(NotificationOutboxDispatchOptions.AzureQueueMode);
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutboxItem?)null);

        await sut.RunAsync(JsonSerializer.Serialize(message), _functionContextMock.Object);

        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_PayloadDeserializationFails_MarksDeadLettered()
    {
        var sut = CreateSut(NotificationOutboxDispatchOptions.AzureQueueMode);
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", "{ invalid payload }", NotificationDispatchStatus.Processing, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        await sut.RunAsync(JsonSerializer.Serialize(message), _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.MarkDeadLetteredAsync(message.NotificationOutboxId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_Success_DispatchesAndMarksSent()
    {
        var sut = CreateSut(NotificationOutboxDispatchOptions.AzureQueueMode);
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var request = new NotificationRequested(new NotificationType("App", "Type"), "corr", new NotificationActor("sys", "App", null, null, null), Array.Empty<NotificationRecipient>(), new Dictionary<string, string>());
        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", JsonSerializer.Serialize(request), NotificationDispatchStatus.Processing, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        await sut.RunAsync(JsonSerializer.Serialize(message), _functionContextMock.Object);

        _notificationServiceMock.Verify(x => x.PublishAsync(It.Is<NotificationRequested>(r => r.CorrelationId == "corr"), It.IsAny<CancellationToken>()), Times.Once);
        _outboxStoreMock.Verify(x => x.MarkSentAsync(message.NotificationOutboxId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DispatcherThrows_MarksFailedAndRethrows()
    {
        var sut = CreateSut(NotificationOutboxDispatchOptions.AzureQueueMode);
        var message = new NotificationQueueMessage { NotificationOutboxId = Guid.NewGuid(), SourceApplication = "App", NotificationType = "Type" };
        var request = new NotificationRequested(new NotificationType("App", "Type"), "corr", new NotificationActor("sys", "App", null, null, null), Array.Empty<NotificationRecipient>(), new Dictionary<string, string>());
        var outboxItem = new NotificationOutboxItem(message.NotificationOutboxId, "App", "Type", "key", JsonSerializer.Serialize(request), NotificationDispatchStatus.Processing, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(message.NotificationOutboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _notificationServiceMock.Setup(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dispatcher failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunAsync(JsonSerializer.Serialize(message), _functionContextMock.Object));

        _outboxStoreMock.Verify(x => x.MarkFailedAsync(message.NotificationOutboxId, "Dispatcher failure", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private SendNotificationQueueTrigger CreateSut(string mode)
    {
        var options = Options.Create(new NotificationOutboxDispatchOptions
        {
            DispatcherMode = mode,
            MaxAttempts = 5,
            RetryDelaySeconds = 300,
            ProcessingLockSeconds = 300
        });
        var dispatcher = new NotificationOutboxDispatcher(
            _outboxStoreMock.Object,
            _notificationServiceMock.Object,
            options,
            NullLogger<NotificationOutboxDispatcher>.Instance);

        return new SendNotificationQueueTrigger(
            dispatcher,
            options,
            NullLogger<SendNotificationQueueTrigger>.Instance);
    }
}
