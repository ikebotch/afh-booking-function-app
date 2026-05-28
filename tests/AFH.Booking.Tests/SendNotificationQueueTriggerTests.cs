using System.Text.Json;
using AFH.Booking.Function.Functions.V1.Notifications.Dispatch;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Services;
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
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly Mock<INotificationOutboxStore> _outboxStoreMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<FunctionContext> _functionContextMock = new();

    public SendNotificationQueueTriggerTests()
    {
        _functionContextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_ProcessesOutboxIdOnlyMessage()
    {
        var sut = CreateSut();
        var outboxId = Guid.NewGuid();
        var outboxItem = CreateOutboxItem(outboxId, JsonSerializer.Serialize(CreateRequest("corr")));

        _outboxStoreMock.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(outboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        await sut.RunAsync(JsonSerializer.Serialize(new NotificationQueueMessage { OutboxId = outboxId }, SerializerOptions), _functionContextMock.Object);

        _notificationServiceMock.Verify(x => x.PublishAsync(It.Is<NotificationRequested>(r => r.CorrelationId == "corr"), outboxId, It.IsAny<CancellationToken>()), Times.Once);
        _outboxStoreMock.Verify(x => x.MarkSentAsync(outboxId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeserializationFails_ThrowsException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<JsonException>(() => sut.RunAsync("{ invalid json }", _functionContextMock.Object));
    }

    [Fact]
    public async Task RunAsync_OutboxItemMissing_ExitsSafely()
    {
        var sut = CreateSut();
        var outboxId = Guid.NewGuid();

        _outboxStoreMock.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutboxItem?)null);

        await sut.RunAsync(JsonSerializer.Serialize(new NotificationQueueMessage { OutboxId = outboxId }, SerializerOptions), _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.TryMarkProcessingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ClaimFailure_ExitsSafely()
    {
        var sut = CreateSut();
        var outboxId = Guid.NewGuid();
        var outboxItem = CreateOutboxItem(outboxId, JsonSerializer.Serialize(CreateRequest("corr")));

        _outboxStoreMock.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(outboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutboxItem?)null);

        await sut.RunAsync(JsonSerializer.Serialize(new NotificationQueueMessage { OutboxId = outboxId }, SerializerOptions), _functionContextMock.Object);

        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_InvalidPersistedPayload_MarksDeadLettered()
    {
        var sut = CreateSut();
        var outboxId = Guid.NewGuid();
        var outboxItem = CreateOutboxItem(outboxId, "{ invalid payload }");

        _outboxStoreMock.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(outboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        await sut.RunAsync(JsonSerializer.Serialize(new NotificationQueueMessage { OutboxId = outboxId }, SerializerOptions), _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.MarkDeadLetteredAsync(outboxId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_DeliverySuccess_MarksSent()
    {
        var sut = CreateSut();
        var outboxId = Guid.NewGuid();
        var outboxItem = CreateOutboxItem(outboxId, JsonSerializer.Serialize(CreateRequest("corr")));

        _outboxStoreMock.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(outboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);

        await sut.RunAsync(JsonSerializer.Serialize(new NotificationQueueMessage { OutboxId = outboxId }, SerializerOptions), _functionContextMock.Object);

        _outboxStoreMock.Verify(x => x.MarkSentAsync(outboxId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeliveryFailure_MarksFailedAndRethrows()
    {
        var sut = CreateSut();
        var outboxId = Guid.NewGuid();
        var outboxItem = CreateOutboxItem(outboxId, JsonSerializer.Serialize(CreateRequest("corr")));

        _outboxStoreMock.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _outboxStoreMock.Setup(x => x.TryMarkProcessingAsync(outboxId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxItem);
        _notificationServiceMock.Setup(x => x.PublishAsync(It.IsAny<NotificationRequested>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dispatcher failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RunAsync(JsonSerializer.Serialize(new NotificationQueueMessage { OutboxId = outboxId }, SerializerOptions), _functionContextMock.Object));

        _outboxStoreMock.Verify(x => x.MarkFailedAsync(outboxId, "Dispatcher failure", It.IsAny<CancellationToken>()), Times.Once);
    }

    private SendNotificationQueueTrigger CreateSut()
    {
        var dispatcher = new NotificationOutboxDispatcher(
            _outboxStoreMock.Object,
            _notificationServiceMock.Object,
            NullLogger<NotificationOutboxDispatcher>.Instance);

        return new SendNotificationQueueTrigger(
            dispatcher,
            NullLogger<SendNotificationQueueTrigger>.Instance);
    }

    private static NotificationRequested CreateRequest(string correlationId)
        => new(
            new NotificationType("App", "Type"),
            correlationId,
            new NotificationActor("sys", "App", null, null, null),
            Array.Empty<NotificationRecipient>(),
            new Dictionary<string, string>());

    private static NotificationOutboxItem CreateOutboxItem(Guid outboxId, string payloadJson)
        => new(outboxId, "App", "Type", "key", payloadJson, NotificationDispatchStatus.Processing, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, null);
}
