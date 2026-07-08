using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Domain.Client;
using AFH.Booking.Infrastructure.Approvals;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public sealed class DbApprovalWorkflowStoreTests
{
    [Fact]
    public async Task ListAsync_FiltersByMultipleStatusesAndChangeTypes()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var store = new DbApprovalWorkflowStore(db);

        var results = await store.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: "adv-1",
            BookingIds: [],
            Statuses: ["Pending", "Rejected"],
            ChangeTypes: ["Cancel", "Rearrange"]), CancellationToken.None);

        Assert.Equal(["request-3", "request-1"], results.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task ListAsync_FiltersByMultipleBookingIds()
    {
        await using var db = CreateDbContext();
        await SeedAsync(db);
        var store = new DbApprovalWorkflowStore(db);

        var results = await store.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: "adv-1",
            BookingIds: ["booking-1", "booking-3"],
            Statuses: [],
            ChangeTypes: []), CancellationToken.None);

        Assert.Equal(["request-3", "request-1"], results.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task ListAsync_EnrichesClientNameFromClientDirectoryWhenTransactionSnapshotIsEmpty()
    {
        await using var db = CreateDbContext();
        await db.BookingTransactions.AddAsync(new BookingTransactionModel
        {
            Id = "tx-1",
            TransactionRef = "client-ref-1",
            BookingReference = "booking-ref-1",
            ClientName = null,
            ProposedStartUtc = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 60,
            IsRemote = true,
            MeetingType = "Online Video",
            Status = 0,
            CreatedUtc = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            RowVersion = [1]
        });
        await db.BookingSlots.AddAsync(new BookingSlotModel
        {
            Id = "slot-1",
            TransactionId = "tx-1",
            AdviserId = "adv-1",
            AdviserName = "Alex Adviser",
            StartUtc = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc),
            CreatedUtc = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc)
        });
        await db.Holds.AddAsync(new BookingHoldModel
        {
            Id = "booking-1",
            Reference = "booking-ref-1",
            UserId = "user-1",
            SlotId = "slot-1",
            Status = HoldStatus.Confirmed,
            CreatedUtc = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            HoldExpiresUtc = new DateTime(2026, 7, 1, 9, 15, 0, DateTimeKind.Utc),
            RowVersion = [1]
        });
        await db.ApprovalRequests.AddAsync(Request(
            "request-1",
            "booking-1",
            "Cancel",
            "adv-1",
            "Pending",
            new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var clients = new StubClientDirectory(new ClientDirectoryItem
        {
            FirstName = "Casey",
            LastName = "Client"
        });
        var store = new DbApprovalWorkflowStore(db, clients, NullLogger<DbApprovalWorkflowStore>.Instance);

        var results = await store.ListAsync(new ListApprovalWorkflowRequestsQuery(
            RequesterId: "adv-1",
            BookingIds: [],
            Statuses: ["Pending"],
            ChangeTypes: []), CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("Casey Client", result.ClientName);
        Assert.Equal("Alex Adviser", result.AdviserName);
        Assert.Equal("Online Video", result.MeetingType);
        Assert.Equal("booking-ref-1", result.BookingReference);

        var transaction = await db.BookingTransactions.AsNoTracking().SingleAsync(x => x.Id == "tx-1");
        Assert.Equal("Casey Client", transaction.ClientName);
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
        await db.ApprovalRequests.AddRangeAsync(
            Request("request-1", "booking-1", "Cancel", "adv-1", "Pending", new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc)),
            Request("request-2", "booking-2", "Rearrange", "adv-1", "Approved", new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc)),
            Request("request-3", "booking-3", "Rearrange", "adv-1", "Rejected", new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc)),
            Request("request-4", "booking-4", "Cancel", "adv-2", "Pending", new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc)));

        await db.SaveChangesAsync();
    }

    private static ApprovalRequestModel Request(
        string id,
        string bookingId,
        string changeType,
        string requesterId,
        string status,
        DateTime requestedUtc)
        => new()
        {
            Id = id,
            BookingId = bookingId,
            TransactionId = $"tx-{bookingId}",
            ChangeType = changeType,
            RequestedBy = "Adviser",
            RequesterId = requesterId,
            Status = status,
            RequestedUtc = requestedUtc,
            ReasonCode = "Reason",
            ReasonDetail = "Detail",
            ApproverTargetType = "Manager",
            ApproverTargetValue = "manager-1",
            ApproverTargetDisplayName = "Manager One"
        };

    private sealed class StubClientDirectory(ClientDirectoryItem client) : IClientDirectory
    {
        public Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct)
        {
            return Task.FromResult(transactionIdOrClientId == "client-ref-1" ? client : null);
        }
    }
}
