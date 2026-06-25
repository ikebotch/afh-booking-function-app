using AFH.Booking.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var number = await NextSequenceValueAsync("BookingReferenceNumber", ct);
        return $"BK-{number:0000}-{CreateSuffix(bookingId)}";
    }

    public async Task<string> GenerateApprovalRequestReferenceAsync(string approvalRequestId, CancellationToken ct)
    {
        var number = await NextSequenceValueAsync("ApprovalRequestReferenceNumber", ct);
        return $"REQ-{number:0000}";
    }

    private async Task<long> NextSequenceValueAsync(string sequenceName, CancellationToken ct)
    {
        var sql = $"SELECT CAST(NEXT VALUE FOR dbo.{sequenceName} AS bigint) AS Value";
        return await _db.Database.SqlQueryRaw<long>(sql).SingleAsync(ct);
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
