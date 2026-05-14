using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class AuditImmutabilityTests
{
    [Fact]
    public async Task SaveChangesAsync_BlocksLifecycleEventModification()
    {
        await using var db = CreateDbContext();
        db.LifecycleEvents.Add(new LifecycleEventModel
        {
            Id = "evt-1",
            BookingId = "booking-1",
            EventType = "Booked",
            NewState = "Booked",
            ActorType = "Client",
            OccurredUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var row = await db.LifecycleEvents.SingleAsync();
        row.ActorType = "System";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_BlocksApprovalHistoryDeletion()
    {
        await using var db = CreateDbContext();
        db.ApprovalHistory.Add(new ApprovalHistoryModel
        {
            Id = "hist-1",
            ApprovalRequestId = "approval-1",
            EventType = "Requested",
            ActorType = "Adviser",
            Outcome = "Pending",
            OccurredUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.ApprovalHistory.Remove(await db.ApprovalHistory.SingleAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }
}