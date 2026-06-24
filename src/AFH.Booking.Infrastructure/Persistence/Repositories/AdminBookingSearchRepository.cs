using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Queries;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class AdminBookingSearchRepository : IAdminBookingSearchRepository
{
    private readonly BookingDbContext _db;

    public AdminBookingSearchRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<AdminBookingSearchResult> SearchAsync(SearchAdminBookingsQuery query, CancellationToken ct)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var rows = BuildQuery(query);
        var totalItems = await rows.CountAsync(ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await rows
            .OrderBy(x => x.Slot.StartUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminBookingSearchItem
            {
                BookingId = x.Id,
                SlotId = x.SlotId,
                TransactionId = x.Slot.TransactionId,
                TransactionRef = x.Slot.Transaction.TransactionRef,
                ClientRef = x.UserId,
                AdviserId = x.Slot.AdviserId,
                AdviserName = x.Slot.AdviserName,
                StartUtc = x.Slot.StartUtc,
                EndUtc = x.Slot.EndUtc,
                DurationMinutes = x.Slot.Transaction.DurationMinutes,
                IsRemote = x.Slot.Transaction.IsRemote,
                MeetingType = x.Slot.Transaction.MeetingType,
                LocationRef = x.Slot.LocationRef ?? x.Slot.Transaction.LocationRef,
                Status = x.Status.ToString(),
                CreatedUtc = x.CreatedUtc,
                ConfirmedUtc = x.ConfirmedUtc,
                CancelledUtc = x.CancelledUtc,
                CancelReason = x.CancelReason
            })
            .ToListAsync(ct);

        return new AdminBookingSearchResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    private IQueryable<BookingHoldModel> BuildQuery(SearchAdminBookingsQuery query)
    {
        var rows = _db.Holds
            .AsNoTracking()
            .Include(x => x.Slot)
            .ThenInclude(x => x.Transaction)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.BookingId))
        {
            var bookingId = query.BookingId.Trim();
            rows = rows.Where(x => x.Id == bookingId);
        }

        if (!string.IsNullOrWhiteSpace(query.TransactionId))
        {
            var transactionId = query.TransactionId.Trim();
            rows = rows.Where(x => x.Slot.TransactionId == transactionId);
        }

        if (!string.IsNullOrWhiteSpace(query.TransactionRef))
        {
            var transactionRef = query.TransactionRef.Trim();
            rows = rows.Where(x => x.Slot.Transaction.TransactionRef == transactionRef);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<HoldStatus>(query.Status.Trim(), true, out var status))
        {
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.AdviserId))
        {
            var adviserId = query.AdviserId.Trim();
            rows = rows.Where(x => x.Slot.AdviserId == adviserId);
        }

        if (!string.IsNullOrWhiteSpace(query.ClientRef))
        {
            var clientRef = query.ClientRef.Trim();
            rows = rows.Where(x => x.UserId == clientRef);
        }

        if (!string.IsNullOrWhiteSpace(query.LocationRef))
        {
            var locationRef = query.LocationRef.Trim();
            rows = rows.Where(x => x.Slot.LocationRef == locationRef || x.Slot.Transaction.LocationRef == locationRef);
        }

        if (!string.IsNullOrWhiteSpace(query.MeetingType))
        {
            var meetingType = query.MeetingType.Trim();
            rows = rows.Where(x => x.Slot.Transaction.MeetingType == meetingType);
        }

        if (query.FromUtc.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc);
            rows = rows.Where(x => x.Slot.StartUtc >= fromUtc);
        }

        if (query.ToUtc.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc);
            rows = rows.Where(x => x.Slot.StartUtc <= toUtc);
        }

        return rows;
    }
}
