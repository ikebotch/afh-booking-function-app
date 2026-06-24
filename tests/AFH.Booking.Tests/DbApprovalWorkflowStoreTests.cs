using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Infrastructure.Approvals;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

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
}
