using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.V1.Dtos;
using Moq;

namespace AFH.Booking.Tests;

public sealed class NotificationAdminServiceTests
{
    [Fact]
    public async Task TemplateAdmin_CreateAsync_RejectsDuplicateKeyVersionChannel()
    {
        var store = new Mock<INotificationTemplateAdminStore>();
        store.Setup(x => x.ExistsAsync("booking-confirmed", "v1", NotificationChannel.Email, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new NotificationTemplateAdminService(store.Object);

        var ex = await Assert.ThrowsAsync<NotificationRequestValidationException>(() =>
            sut.CreateAsync(CreateTemplate(), CancellationToken.None));

        Assert.Contains("unique", ex.Message, StringComparison.OrdinalIgnoreCase);
        store.Verify(x => x.CreateAsync(It.IsAny<NotificationTemplateUpsert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TemplateAdmin_CreateAsync_EmailRequiresSubject()
    {
        var sut = new NotificationTemplateAdminService(Mock.Of<INotificationTemplateAdminStore>());

        var ex = await Assert.ThrowsAsync<NotificationRequestValidationException>(() =>
            sut.CreateAsync(CreateTemplate(subject: ""), CancellationToken.None));

        Assert.Contains("SubjectTemplate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TemplateAdmin_CreateAsync_SmsAllowsMissingSubject()
    {
        var store = new Mock<INotificationTemplateAdminStore>();
        var created = CreateTemplate(channel: NotificationChannel.Sms, subject: null);
        store.Setup(x => x.CreateAsync(It.IsAny<NotificationTemplateUpsert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationTemplateAdminItem(
                Guid.NewGuid(),
                created.TemplateKey,
                created.TemplateVersion,
                NotificationChannel.Sms,
                created.Name,
                created.Description,
                null,
                created.BodyTemplate,
                created.ContentType,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow));
        var sut = new NotificationTemplateAdminService(store.Object);

        await sut.CreateAsync(created, CancellationToken.None);

        store.Verify(x => x.CreateAsync(It.Is<NotificationTemplateUpsert>(template =>
            template.Channel == NotificationChannel.Sms &&
            template.SubjectTemplate == null &&
            template.ContentType == "text/plain"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TemplateAdmin_CreateAsync_SmsRequiresTextPlain()
    {
        var sut = new NotificationTemplateAdminService(Mock.Of<INotificationTemplateAdminStore>());

        var ex = await Assert.ThrowsAsync<NotificationRequestValidationException>(() =>
            sut.CreateAsync(CreateTemplate(channel: NotificationChannel.Sms, subject: null, contentType: "text/html"), CancellationToken.None));

        Assert.Contains("text/plain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TemplatePreview_Override_DoesNotCreateOutboxOrSend()
    {
        var store = new Mock<INotificationTemplateStore>();
        var sut = new NotificationTemplatePreviewService(store.Object);

        var result = await sut.PreviewAsync(new NotificationTemplatePreviewRequest(
            "booking-confirmed",
            "v1",
            NotificationChannel.Email,
            "Hello {{clientName}}",
            "Body {{missingToken}}",
            "text/plain",
            new Dictionary<string, string> { ["clientName"] = "John" }), CancellationToken.None);

        Assert.Equal("Hello John", result.Subject);
        Assert.Equal("Body ", result.Body);
        Assert.Equal(["missingToken"], result.MissingTokens);
        store.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TemplatePreview_ExistingTemplate_RendersFromStore()
    {
        var store = new Mock<INotificationTemplateStore>();
        store.Setup(x => x.GetAsync("booking-confirmed", "v1", NotificationChannel.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationTemplateDefinition(
                "booking-confirmed",
                "v1",
                NotificationChannel.Email,
                "Booking confirmed",
                null,
                "Hello {{clientName}}",
                "Body {{appointmentDate}}",
                "text/plain",
                true));
        var sut = new NotificationTemplatePreviewService(store.Object);

        var result = await sut.PreviewAsync(new NotificationTemplatePreviewRequest(
            "booking-confirmed",
            "v1",
            NotificationChannel.Email,
            null,
            null,
            "text/plain",
            new Dictionary<string, string>
            {
                ["clientName"] = "John",
                ["appointmentDate"] = "2026-05-28"
            }), CancellationToken.None);

        Assert.Equal("Hello John", result.Subject);
        Assert.Equal("Body 2026-05-28", result.Body);
        Assert.Empty(result.MissingTokens);
    }

    [Fact]
    public async Task TemplatePreview_MissingTemplate_FailsClearly()
    {
        var sut = new NotificationTemplatePreviewService(Mock.Of<INotificationTemplateStore>());

        var ex = await Assert.ThrowsAsync<NotificationRequestValidationException>(() =>
            sut.PreviewAsync(new NotificationTemplatePreviewRequest(
                "missing",
                "v1",
                NotificationChannel.Email,
                null,
                null,
                "text/plain",
                new Dictionary<string, string>()), CancellationToken.None));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminOperation_Requeue_FailedNotification_EnqueuesOutboxIdOnly()
    {
        var outboxId = Guid.NewGuid();
        var outboxStore = new Mock<INotificationOutboxStore>();
        var queue = new Mock<INotificationQueuePublisher>();
        NotificationQueueMessage? captured = null;
        outboxStore.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxItem(outboxId, "Booking", "BookingConfirmed", "key", "{}", NotificationDispatchStatus.Failed, null, 1, "failed", DateTime.UtcNow, DateTime.UtcNow, null));
        queue.Setup(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationQueueMessage, CancellationToken>((message, _) => captured = message)
            .ReturnsAsync(new NotificationQueuePublishResult("queue-message"));
        var sut = new NotificationAdminOperationService(outboxStore.Object, queue.Object);

        var result = await sut.RequeueAsync(outboxId, CancellationToken.None);

        Assert.Equal(NotificationDispatchStatus.Queued.ToString(), result.Status);
        Assert.NotNull(captured);
        Assert.Equal(outboxId, captured!.OutboxId);
        Assert.Equal(["OutboxId"], typeof(NotificationQueueMessage).GetProperties().Select(x => x.Name));
        outboxStore.Verify(x => x.MarkRequeuedAsync(outboxId, "queue-message", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminOperation_Requeue_SentNotification_IsRejected()
    {
        var outboxId = Guid.NewGuid();
        var outboxStore = new Mock<INotificationOutboxStore>();
        var queue = new Mock<INotificationQueuePublisher>();
        outboxStore.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxItem(outboxId, "Booking", "BookingConfirmed", "key", "{}", NotificationDispatchStatus.Sent, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow));
        var sut = new NotificationAdminOperationService(outboxStore.Object, queue.Object);

        await Assert.ThrowsAsync<NotificationRequestValidationException>(() => sut.RequeueAsync(outboxId, CancellationToken.None));

        queue.Verify(x => x.PublishAsync(It.IsAny<NotificationQueueMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminOperation_DeadLetter_SentNotification_IsRejected()
    {
        var outboxId = Guid.NewGuid();
        var outboxStore = new Mock<INotificationOutboxStore>();
        outboxStore.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxItem(outboxId, "Booking", "BookingConfirmed", "key", "{}", NotificationDispatchStatus.Sent, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow));
        var sut = new NotificationAdminOperationService(outboxStore.Object, Mock.Of<INotificationQueuePublisher>());

        await Assert.ThrowsAsync<NotificationRequestValidationException>(() => sut.DeadLetterAsync(outboxId, "reason", CancellationToken.None));

        outboxStore.Verify(x => x.MarkDeadLetteredAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminOperation_MarkFailed_SentNotification_IsRejected()
    {
        var outboxId = Guid.NewGuid();
        var outboxStore = new Mock<INotificationOutboxStore>();
        outboxStore.Setup(x => x.GetAsync(outboxId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOutboxItem(outboxId, "Booking", "BookingConfirmed", "key", "{}", NotificationDispatchStatus.Sent, null, 1, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow));
        var sut = new NotificationAdminOperationService(outboxStore.Object, Mock.Of<INotificationQueuePublisher>());

        await Assert.ThrowsAsync<NotificationRequestValidationException>(() => sut.MarkFailedAsync(outboxId, "reason", CancellationToken.None));

        outboxStore.Verify(x => x.MarkFailedFromAdminAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static NotificationTemplateUpsert CreateTemplate(
        string? subject = "Subject",
        NotificationChannel channel = NotificationChannel.Email,
        string contentType = "text/plain")
        => new(
            "booking-confirmed",
            "v1",
            channel,
            "Booking confirmed",
            null,
            subject,
            "Body",
            contentType,
            true,
            "test");
}
