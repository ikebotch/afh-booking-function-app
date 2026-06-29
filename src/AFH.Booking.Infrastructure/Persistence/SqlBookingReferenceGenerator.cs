using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class SqlBookingReferenceGenerator : IBookingReferenceGenerator
{
    private readonly BookingDbContext _db;

    public SqlBookingReferenceGenerator(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateBookingReferenceAsync(string bookingId, CancellationToken ct)
    {
        var number = await AllocateBookingReferenceNumberAsync(ct);
        return $"BK-{number:0000}-{CreateSuffix(bookingId)}";
    }

    public async Task<string> GenerateApprovalRequestReferenceAsync(string approvalRequestId, CancellationToken ct)
    {
        var number = await AllocateApprovalRequestReferenceNumberAsync(ct);
        return $"REQ-{number:0000}";
    }

    private async Task<long> AllocateBookingReferenceNumberAsync(CancellationToken ct)
    {
        var allocation = new BookingReferenceAllocationModel
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow
        };

        _db.BookingReferenceAllocations.Add(allocation);
        await _db.SaveChangesAsync(ct);
        return allocation.Value;
    }

    private async Task<long> AllocateApprovalRequestReferenceNumberAsync(CancellationToken ct)
    {
        var allocation = new ApprovalRequestReferenceAllocationModel
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow
        };

        _db.ApprovalRequestReferenceAllocations.Add(allocation);
        await _db.SaveChangesAsync(ct);
        return allocation.Value;
    }

    private static string CreateSuffix(string value)
    {
        var source = string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value;

        var suffix = new string(source
            .Where(char.IsLetterOrDigit)
            .Take(4)
            .ToArray());

        return string.IsNullOrWhiteSpace(suffix)
            ? Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()
            : suffix.ToUpperInvariant();
    }
}
