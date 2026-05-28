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

        await store.RecordAttemptAsync(new NotificationDeliveryAuditRecord(
            Id: "dispatch-1",
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
    }

    private static NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NotificationDbContext(options);
    }
}
