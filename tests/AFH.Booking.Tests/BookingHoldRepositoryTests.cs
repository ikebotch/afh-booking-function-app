using AFH.Booking.Domain.Bookings;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class BookingHoldRepositoryTests
{
    [Fact]
    public async Task GetExpiredActiveAsync_ExcludesExpiredHoldWithPendingApprovalRequest()
    {
        var utcNow = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();

        SeedHold(db, "hold-protected", "slot-protected", "tx-protected", utcNow, utcNow.AddMinutes(-10));
        SeedHold(db, "hold-expired", "slot-expired", "tx-expired", utcNow, utcNow.AddMinutes(-10));
        SeedPendingApproval(db, "approval-1", "hold-protected", "tx-protected");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sut = new BookingHoldRepository(db);

        var result = await sut.GetExpiredActiveAsync(utcNow, take: 10, CancellationToken.None);

        var hold = Assert.Single(result);
        Assert.Equal("hold-expired", hold.Id);
    }

    [Fact]
    public async Task GetActiveBySlotIdAsync_ReturnsExpiredHoldWithPendingApprovalRequest()
    {
        var utcNow = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();

        SeedHold(db, "hold-protected", "slot-protected", "tx-protected", utcNow, utcNow.AddMinutes(-10));
        SeedPendingApproval(db, "approval-1", "hold-protected", "tx-protected");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sut = new BookingHoldRepository(db);

        var result = await sut.GetActiveBySlotIdAsync("slot-protected", utcNow, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("hold-protected", result!.Id);
    }

    [Fact]
    public async Task CountActiveOrConfirmedByAdviserAsync_CountsExpiredHoldWithPendingApprovalRequest()
    {
        var utcNow = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();

        SeedHold(db, "hold-protected", "slot-protected", "tx-protected", utcNow, utcNow.AddMinutes(-10));
        SeedPendingApproval(db, "approval-1", "hold-protected", "tx-protected");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sut = new BookingHoldRepository(db);

        var result = await sut.CountActiveOrConfirmedByAdviserAsync(
            "adv-1",
            utcNow.AddMinutes(25),
            utcNow.AddMinutes(65),
            utcNow,
            CancellationToken.None);

        Assert.Equal(1, result);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }

    private static void SeedHold(
        BookingDbContext db,
        string holdId,
        string slotId,
        string transactionId,
        DateTime utcNow,
        DateTime holdExpiresUtc)
    {
        var transaction = new BookingTransactionModel
        {
            Id = transactionId,
            TransactionRef = $"TRX-{transactionId}",
            ProposedStartUtc = utcNow.AddMinutes(30),
            DurationMinutes = 30,
            Timezone = "UTC",
            IsRemote = true,
            MeetingType = "Review",
            Status = (int)BookingTransactionStatus.Open,
            CreatedUtc = utcNow.AddMinutes(-30),
            ExpiresUtc = utcNow.AddMinutes(30),
            RowVersion = [1]
        };

        var slot = new BookingSlotModel
        {
            Id = slotId,
            TransactionId = transaction.Id,
            AdviserId = "adv-1",
            AdviserName = "Adviser One",
            StartUtc = utcNow.AddMinutes(30),
            EndUtc = utcNow.AddMinutes(60),
            Score = 10,
            CreatedUtc = utcNow.AddMinutes(-20),
            Transaction = transaction
        };

        var hold = new BookingHoldModel
        {
            Id = holdId,
            UserId = "user-1",
            SlotId = slot.Id,
            Slot = slot,
            Status = HoldStatus.Active,
            CreatedUtc = utcNow.AddMinutes(-15),
            HoldExpiresUtc = holdExpiresUtc,
            RowVersion = [1]
        };

        transaction.Slots.Add(slot);
        slot.Hold = hold;

        db.BookingTransactions.Add(transaction);
        db.BookingSlots.Add(slot);
        db.Holds.Add(hold);
    }

    private static void SeedPendingApproval(
        BookingDbContext db,
        string approvalId,
        string holdId,
        string transactionId)
    {
        db.ApprovalRequests.Add(new ApprovalRequestModel
        {
            Id = approvalId,
            BookingId = holdId,
            TransactionId = transactionId,
            ChangeType = "Cancel",
            RequestedBy = "Client",
            Status = "Pending",
            RequestedUtc = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc),
            ApproverTargetType = "Role",
            ApproverTargetValue = "Manager",
            ApproverTargetDisplayName = "Manager"
        });
    }
}
