using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AFH.Booking.Tests;

public sealed class NotificationRequestIngestionServiceTests
{
    private readonly Mock<INotificationOutboxStore> _outboxStoreMock = new();
    private readonly Mock<INotificationQueuePublisher> _queuePublisherMock = new();

    [Fact]
    public async Task AcceptAsync_CreatesOutbox_AndEnqueuesOutboxIdOnlyMessage()
    {
        var sut = CreateSut();
        var request = CreateRequest(correlationId: "corr-123");
        NotificationQueueMessage? capturedMessage = null;

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutboxItem item, CancellationToken _) => new NotificationOutboxCreateResult(item, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationQueueMessage, CancellationToken>((message, _) => capturedMessage = message)
            .ReturnsAsync(new NotificationQueuePublishResult("queue-message"));

        var result = await sut.AcceptAsync(request, CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal("corr-123", result.CorrelationId);
        Assert.NotEqual(Guid.Empty, result.NotificationRequestId);
        Assert.NotNull(capturedMessage);
        Assert.Equal(result.NotificationRequestId, capturedMessage!.OutboxId);
        Assert.Equal(["OutboxId"], typeof(NotificationQueueMessage).GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task AcceptAsync_DuplicateRequest_ReturnsAcceptedWithoutEnqueue()
    {
        var sut = CreateSut();
        var existingOutboxId = Guid.NewGuid();
        var existing = new NotificationOutboxItem(existingOutboxId, "Booking", "BookingConfirmed", "key", "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxCreateResult(existing, false));

        var result = await sut.AcceptAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(existingOutboxId, result.NotificationRequestId);
        Assert.Equal("Accepted", result.Status);
        Assert.False(result.Created);
        _queuePublisherMock.Verify(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_GeneratesCorrelationId_WhenMissing()
    {
        var sut = CreateSut();
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutboxItem item, CancellationToken _) => new NotificationOutboxCreateResult(item, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationQueuePublishResult("queue-message"));

        var result = await sut.AcceptAsync(CreateRequest(correlationId: ""), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.CorrelationId));
    }

    [Fact]
    public async Task AcceptAsync_RejectsMissingTemplate()
    {
        var sut = CreateSut();
        var request = CreateRequest(data: new Dictionary<string, string>());

        var ex = await Assert.ThrowsAsync<NotificationRequestValidationException>(() => sut.AcceptAsync(request, CancellationToken.None));

        Assert.Contains("TemplateKey and TemplateVersion", ex.Message);
    }

    [Fact]
    public async Task AcceptAsync_SerializesInternalOutboxPayload_ButInternalQueueHasOutboxIdOnly()
    {
        var sut = CreateSut();
        NotificationOutboxItem? capturedOutbox = null;
        NotificationQueueMessage? capturedMessage = null;
        _outboxStoreMock.Setup(x => x.CreateOrGetAsync(It.IsAny<NotificationOutboxItem>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOutboxItem, CancellationToken>((item, _) => capturedOutbox = item)
            .ReturnsAsync((NotificationOutboxItem item, CancellationToken _) => new NotificationOutboxCreateResult(item, true));
        _queuePublisherMock.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationQueueMessage, CancellationToken>((message, _) => capturedMessage = message)
            .ReturnsAsync(new NotificationQueuePublishResult("queue-message"));

        await sut.AcceptAsync(CreateRequest(), CancellationToken.None);

        Assert.NotNull(capturedOutbox);
        Assert.Contains("client@example.com", capturedOutbox!.PayloadJson, StringComparison.Ordinal);
        Assert.NotNull(capturedMessage);
        var queueJson = JsonSerializer.Serialize(capturedMessage, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal($$"""{"outboxId":"{{capturedMessage!.OutboxId}}"}""", queueJson);
    }

    private NotificationRequestIngestionService CreateSut()
    {
        var outbox = new NotificationOutboxService(
            _outboxStoreMock.Object,
            _queuePublisherMock.Object,
            new NotificationIdempotencyKeyGenerator([new BookingNotificationIdempotencyPolicy()]),
            new NotificationRecipientResolver([new BookingNotificationRoutingPolicy()]),
            NullLogger<NotificationOutboxService>.Instance);

        return new NotificationRequestIngestionService(outbox);
    }

    private static NotificationRequested CreateRequest(string correlationId = "corr-123", IReadOnlyDictionary<string, string>? data = null)
        => new(
            new NotificationType("Booking", "BookingConfirmed"),
            correlationId,
            new NotificationActor("System", "Booking", null, null, null),
            [new NotificationRecipient("Client", "Client", "client@example.com", null, null, [NotificationChannel.Email])],
            data ?? new Dictionary<string, string>
            {
                ["TemplateKey"] = "booking-confirmed",
                ["TemplateVersion"] = "v1",
                ["bookingId"] = "booking-123"
            });
}
