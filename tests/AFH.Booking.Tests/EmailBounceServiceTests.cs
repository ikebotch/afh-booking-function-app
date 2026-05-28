using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Infrastructure.Clients;
using AFH.Notification.Infrastructure.Bouncebacks;
using AFH.Notification.Infrastructure.Persistence;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFH.Booking.Tests;

public class EmailBounceServiceTests
{
    [Fact]
    public async Task RecordBounceAsync_UpdatesQueuedDispatchAuditRecord()
    {
        await using var db = CreateDbContext();
        var outboxId = Guid.NewGuid();
        db.NotificationDispatches.Add(CreateDispatch(
            providerMessageId: "queued-provider-1",
            notificationOutboxId: outboxId,
            sourceApplication: "Booking",
            notificationType: "BookingConfirmed",
            channel: "Email",
            providerName: "Graph"));
        await db.SaveChangesAsync();

        var sut = CreateService(db);

        await sut.RecordBounceAsync(new EmailBounceWebhookRequest
        {
            ProviderMessageId = "queued-provider-1",
            ReasonCode = "Bounced",
            ReasonDetail = "Mailbox unavailable",
            OccurredUtc = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);

        var dispatch = await db.NotificationDispatches.SingleAsync(x => x.ProviderMessageId == "queued-provider-1");
        Assert.Equal("Bounced", dispatch.EmailStatus);
        Assert.Equal(outboxId, dispatch.NotificationOutboxId);
        Assert.Equal("Graph", dispatch.ProviderName);
        Assert.NotNull(dispatch.UpdatedUtc);
        Assert.Single(await db.EmailBounceEvents.ToListAsync());
    }

    [Fact]
    public async Task RecordBounceAsync_UpdatesLegacyDispatchAuditRecord()
    {
        await using var db = CreateDbContext();
        db.NotificationDispatches.Add(CreateDispatch(providerMessageId: "legacy-provider-1"));
        await db.SaveChangesAsync();

        var sut = CreateService(db);

        await sut.RecordBounceAsync(new EmailBounceWebhookRequest
        {
            ProviderMessageId = "legacy-provider-1",
            ReasonCode = "Bounced",
            ReasonDetail = "Legacy message bounced"
        }, CancellationToken.None);

        var dispatch = await db.NotificationDispatches.SingleAsync(x => x.ProviderMessageId == "legacy-provider-1");
        Assert.Equal("Bounced", dispatch.EmailStatus);
        Assert.Null(dispatch.NotificationOutboxId);
        Assert.Single(await db.EmailBounceEvents.ToListAsync());
    }

    [Fact]
    public async Task RecordBounceAsync_WithUnknownProviderMessageId_RecordsEventWithoutThrowing()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        await sut.RecordBounceAsync(new EmailBounceWebhookRequest
        {
            ProviderMessageId = "unknown-provider-1",
            ReasonCode = "Bounced",
            ReasonDetail = "Unknown message"
        }, CancellationToken.None);

        var bounceEvent = await db.EmailBounceEvents.SingleAsync();
        Assert.Equal("unknown-provider-1", bounceEvent.ProviderMessageId);
        Assert.Empty(await db.NotificationDispatches.ToListAsync());
    }

    private static NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new NotificationDbContext(options);
    }

    private static EmailBounceService CreateService(NotificationDbContext db)
        => new(new EmailBouncebackStore(db, NullLogger<EmailBouncebackStore>.Instance));

    private static NotificationDispatchModel CreateDispatch(
        string providerMessageId,
        Guid? notificationOutboxId = null,
        string? sourceApplication = null,
        string? notificationType = null,
        string? channel = null,
        string? providerName = null)
    {
        return new NotificationDispatchModel
        {
            Id = Guid.NewGuid().ToString("N"),
            BookingId = "booking-1",
            EventType = notificationType ?? "Booked",
            SmsRequested = false,
            EmailRequested = true,
            SmsStatus = "Skipped",
            EmailStatus = "Sent",
            OutcomeCode = "Delivered",
            RecipientEmail = "client@example.com",
            ProviderMessageId = providerMessageId,
            NotificationOutboxId = notificationOutboxId,
            SourceApplication = sourceApplication,
            NotificationType = notificationType,
            Channel = channel,
            ProviderName = providerName,
            CreatedUtc = DateTime.UtcNow
        };
    }
}
