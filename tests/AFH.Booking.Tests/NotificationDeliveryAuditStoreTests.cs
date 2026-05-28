using System.Security.Cryptography;
using System.Text;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class NotificationDeliveryAuditStoreTests
{
    [Fact]
    public async Task RecordAttemptAsync_QueuedDispatchWritesNeutralAuditColumnsOnly()
    {
        await using var db = CreateDbContext();
        var store = new NotificationDeliveryAuditStore(db);
        var outboxId = Guid.NewGuid();
        var dispatchId = Guid.NewGuid();

        await store.RecordAttemptAsync(new NotificationDeliveryAuditRecord(
            Id: "dispatch-1",
            DispatchUid: dispatchId,
            NotificationOutboxId: outboxId,
            SourceApplication: "Booking",
            SourceReferenceType: "Booking",
            SourceReferenceId: "booking-1",
            NotificationType: "BookingConfirmed",
            Channel: "Email",
            RecipientType: "Client",
            RecipientEmail: "client@example.test",
            RecipientMobile: null,
            ProviderName: "Graph",
            Status: "Sent",
            ProviderMessageId: "graph-correlation-1",
            FailureDetails: null,
            CorrelationId: "correlation-1",
            TemplateKey: "booking-confirmed",
            TemplateVersion: "v1",
            CreatedUtc: new DateTime(2026, 5, 28, 9, 0, 0, DateTimeKind.Utc),
            UpdatedUtc: new DateTime(2026, 5, 28, 9, 0, 1, DateTimeKind.Utc)),
            CancellationToken.None);

        var dispatch = await db.NotificationDispatches.SingleAsync();
        Assert.Equal(dispatchId, dispatch.DispatchUid);
        Assert.Equal(outboxId, dispatch.NotificationOutboxId);
        Assert.Equal("Booking", dispatch.SourceApplication);
        Assert.Equal("Booking", dispatch.SourceReferenceType);
        Assert.Equal("booking-1", dispatch.SourceReferenceId);
        Assert.Equal("BookingConfirmed", dispatch.NotificationType);
        Assert.Equal("Client", dispatch.RecipientType);
        Assert.Equal("client@example.test", dispatch.RecipientEmail);
        Assert.Equal("Email", dispatch.Channel);
        Assert.Equal("Graph", dispatch.ProviderName);
        Assert.Equal("graph-correlation-1", dispatch.ProviderMessageId);
        Assert.Equal("booking-confirmed", dispatch.TemplateKey);
        Assert.Equal("v1", dispatch.TemplateVersion);
        Assert.Equal("Sent", dispatch.Status);

        Assert.Null(dispatch.BookingId);
        Assert.Null(dispatch.TransactionId);
        Assert.Null(dispatch.TransactionRef);
        Assert.Null(dispatch.LifecycleEventId);
        Assert.Null(dispatch.EventType);
        Assert.Null(dispatch.SmsRequested);
        Assert.Null(dispatch.EmailRequested);
        Assert.Null(dispatch.SmsStatus);
        Assert.Null(dispatch.EmailStatus);
        Assert.Null(dispatch.OutcomeCode);
        Assert.Null(dispatch.RecipientPhone);
        Assert.Null(dispatch.TemplateName);
        Assert.Empty(await db.NotificationMessageLogs.ToListAsync());
    }

    [Fact]
    public async Task RecordAttemptAsync_WithRenderedContent_WritesMessageLogAndKeepsDispatchLightweight()
    {
        await using var db = CreateDbContext();
        var store = new NotificationDeliveryAuditStore(db);
        var outboxId = Guid.NewGuid();
        var dispatchId = Guid.NewGuid();
        const string body = "Rendered body sent to provider.";
        const string subject = "Rendered subject";

        await store.RecordAttemptAsync(new NotificationDeliveryAuditRecord(
            Id: dispatchId.ToString("N"),
            DispatchUid: dispatchId,
            NotificationOutboxId: outboxId,
            SourceApplication: "Booking",
            SourceReferenceType: "Booking",
            SourceReferenceId: "booking-1",
            NotificationType: "BookingConfirmed",
            Channel: "Email",
            RecipientType: "Client",
            RecipientEmail: "client@example.test",
            RecipientMobile: null,
            ProviderName: "Graph",
            Status: "Sent",
            ProviderMessageId: "graph-correlation-1",
            FailureDetails: null,
            CorrelationId: "correlation-1",
            TemplateKey: "booking-confirmed",
            TemplateVersion: "v1",
            CreatedUtc: new DateTime(2026, 5, 28, 9, 0, 0, DateTimeKind.Utc),
            UpdatedUtc: new DateTime(2026, 5, 28, 9, 0, 1, DateTimeKind.Utc),
            MessageLog: new NotificationMessageLogRecord(
                Guid.NewGuid(),
                dispatchId,
                outboxId,
                "Booking",
                "BookingConfirmed",
                "correlation-1",
                "Client",
                "client@example.test",
                null,
                "Email",
                "booking-confirmed",
                "v1",
                TemplateContentId: null,
                subject,
                body,
                "text/plain",
                """{"bookingId":"booking-1"}""",
                BodyHash: null,
                new DateTime(2026, 5, 28, 9, 0, 0, DateTimeKind.Utc))),
            CancellationToken.None);

        var dispatch = await db.NotificationDispatches.SingleAsync();
        var log = await db.NotificationMessageLogs.SingleAsync();

        Assert.Equal(dispatch.DispatchUid, log.NotificationDispatchId);
        Assert.Equal(outboxId, log.NotificationOutboxId);
        Assert.Equal(subject, log.Subject);
        Assert.Equal(body, log.Body);
        Assert.Equal("booking-confirmed", log.TemplateKey);
        Assert.Equal("v1", log.TemplateVersion);
        Assert.Equal("text/plain", log.ContentType);
        Assert.Equal(ComputeSha256(body), log.BodyHash);
        Assert.Null(dispatch.MessageSubject);
        Assert.Null(dispatch.MessageBody);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NotificationDbContext(options);
    }
}
