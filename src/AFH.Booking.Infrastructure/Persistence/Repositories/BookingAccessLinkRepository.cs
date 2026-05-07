using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class BookingAccessLinkRepository : IBookingAccessLinkRepository
{
    private readonly BookingDbContext _db;

    public BookingAccessLinkRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(BookingAccessLinkRecord link, CancellationToken ct)
    {
        await _db.BookingAccessLinks.AddAsync(new BookingAccessLinkModel
        {
            Id = link.Id,
            OriginalBookingId = link.OriginalBookingId,
            CurrentBookingId = link.CurrentBookingId,
            TokenHash = link.TokenHash,
            ActorType = link.ActorType,
            ActorId = link.ActorId,
            TransactionRef = link.TransactionRef,
            ExpiresUtc = link.ExpiresUtc,
            CreatedUtc = link.CreatedUtc,
            CreatedBy = link.CreatedBy,
            RevokedUtc = link.RevokedUtc,
            RevokedReason = link.RevokedReason
        }, ct);
    }

    public async Task<BookingAccessLinkRecord?> GetAsync(string linkId, CancellationToken ct)
    {
        var row = await _db.BookingAccessLinks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == linkId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task RevokeActiveForBookingAsync(string bookingId, DateTime revokedUtc, string reason, CancellationToken ct)
    {
        var rows = await _db.BookingAccessLinks
            .Where(x => x.CurrentBookingId == bookingId && x.RevokedUtc == null)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            row.RevokedUtc = revokedUtc;
            row.RevokedReason = reason;
        }
    }

    private static BookingAccessLinkRecord ToRecord(BookingAccessLinkModel row) => new()
    {
        Id = row.Id,
        OriginalBookingId = row.OriginalBookingId,
        CurrentBookingId = row.CurrentBookingId,
        TokenHash = row.TokenHash,
        ActorType = row.ActorType,
        ActorId = row.ActorId,
        TransactionRef = row.TransactionRef,
        ExpiresUtc = row.ExpiresUtc,
        CreatedUtc = row.CreatedUtc,
        CreatedBy = row.CreatedBy,
        RevokedUtc = row.RevokedUtc,
        RevokedReason = row.RevokedReason
    };
}
