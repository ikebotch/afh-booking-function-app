using AFH.Booking.Domain.Bookings.Queries;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class AdminBookingSearchRepositoryTests
{
    [Fact]
    public async Task SearchAsync_ReturnsPagedBookingsForAdminList()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var repository = new AdminBookingSearchRepository(db);

        var result = await repository.SearchAsync(new SearchAdminBookingsQuery
        {
            Page = 1,
            PageSize = 2
        }, CancellationToken.None);

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(["booking-2", "booking-1"], result.Items.Select(x => x.BookingId).ToArray());
        Assert.Equal("Confirmed", result.Items[1].Status);
        Assert.Equal("client-1", result.Items[1].ClientRef);
    }

    [Fact]
    public async Task SearchAsync_FiltersByAdviserStatusDateAndClientRef()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var repository = new AdminBookingSearchRepository(db);

        var result = await repository.SearchAsync(new SearchAdminBookingsQuery
        {
            AdviserIds = ["adv-1"],
            Statuses = ["confirmed"],
            ClientRefs = ["client-1"],
            FromUtc = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 7, 15, 23, 59, 59, DateTimeKind.Utc),
            Page = 1,
            PageSize = 25
        }, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("booking-1", item.BookingId);
        Assert.Equal("TRX-1", item.TransactionRef);
        Assert.Equal("Review", item.MeetingType);
    }

    [Fact]
    public async Task SearchAsync_FiltersByMultipleStatusesAndAdvisers()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var repository = new AdminBookingSearchRepository(db);

        var result = await repository.SearchAsync(new SearchAdminBookingsQuery
        {
            Statuses = ["Active", "Cancelled"],
            AdviserIds = ["adv-1", "adv-2"],
            Page = 1,
            PageSize = 25
        }, CancellationToken.None);

        Assert.Equal(["booking-2", "booking-3"], result.Items.Select(x => x.BookingId).ToArray());
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }

    private static async Task SeedAsync(BookingDbContext db)
    {
        var tx1 = Transaction("tx-1", "TRX-1", new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc), "Review", "branch-1");
        var tx2 = Transaction("tx-2", "TRX-2", new DateTime(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc), "Planning", "branch-2");
        var tx3 = Transaction("tx-3", "TRX-3", new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc), "Review", "branch-1");

        var slot1 = Slot("slot-1", tx1.Id, "adv-1", "Ada Adviser", tx1.ProposedStartUtc, "branch-1");
        var slot2 = Slot("slot-2", tx2.Id, "adv-2", "Ben Adviser", tx2.ProposedStartUtc, "branch-2");
        var slot3 = Slot("slot-3", tx3.Id, "adv-1", "Ada Adviser", tx3.ProposedStartUtc, "branch-1");

        await db.BookingTransactions.AddRangeAsync(tx1, tx2, tx3);
        await db.BookingSlots.AddRangeAsync(slot1, slot2, slot3);
        await db.Holds.AddRangeAsync(
            Hold("booking-1", "client-1", slot1.Id, HoldStatus.Confirmed, tx1.ProposedStartUtc.AddDays(-1), tx1.ProposedStartUtc.AddMinutes(-30), null),
            Hold("booking-2", "client-2", slot2.Id, HoldStatus.Active, tx2.ProposedStartUtc.AddDays(-1), null, null),
            Hold("booking-3", "client-1", slot3.Id, HoldStatus.Cancelled, tx3.ProposedStartUtc.AddDays(-1), null, tx3.ProposedStartUtc.AddHours(-2)));
        await db.SaveChangesAsync();
    }

    private static BookingTransactionModel Transaction(string id, string transactionRef, DateTime startUtc, string meetingType, string locationRef)
        => new()
        {
            Id = id,
            TransactionRef = transactionRef,
            ProposedStartUtc = startUtc,
            DurationMinutes = 60,
            Timezone = "Europe/London",
            IsRemote = false,
            MeetingType = meetingType,
            LocationRef = locationRef,
            Status = 1,
            CreatedUtc = startUtc.AddDays(-2),
            ExpiresUtc = startUtc.AddHours(1),
            RowVersion = []
        };

    private static BookingSlotModel Slot(string id, string transactionId, string adviserId, string adviserName, DateTime startUtc, string locationRef)
        => new()
        {
            Id = id,
            TransactionId = transactionId,
            AdviserId = adviserId,
            AdviserName = adviserName,
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1),
            Score = 100,
            LocationRef = locationRef,
            CreatedUtc = startUtc.AddDays(-2)
        };

    private static BookingHoldModel Hold(
        string id,
        string clientRef,
        string slotId,
        HoldStatus status,
        DateTime createdUtc,
        DateTime? confirmedUtc,
        DateTime? cancelledUtc)
        => new()
        {
            Id = id,
            UserId = clientRef,
            SlotId = slotId,
            Status = status,
            CreatedUtc = createdUtc,
            HoldExpiresUtc = createdUtc.AddHours(1),
            ConfirmedUtc = confirmedUtc,
            CancelledUtc = cancelledUtc,
            CancelReason = cancelledUtc.HasValue ? "Client request" : null,
            RowVersion = []
        };
}
