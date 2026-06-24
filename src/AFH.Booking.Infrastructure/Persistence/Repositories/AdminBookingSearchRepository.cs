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

        if (query.BookingIds.Count > 0)
        {
            var bookingIds = Normalize(query.BookingIds);
            rows = rows.Where(x => bookingIds.Contains(x.Id));
        }

        if (query.TransactionIds.Count > 0)
        {
            var transactionIds = Normalize(query.TransactionIds);
            rows = rows.Where(x => transactionIds.Contains(x.Slot.TransactionId));
        }

        if (query.TransactionRefs.Count > 0)
        {
            var transactionRefs = Normalize(query.TransactionRefs);
            rows = rows.Where(x => transactionRefs.Contains(x.Slot.Transaction.TransactionRef));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses
                .Select(status => Enum.Parse<HoldStatus>(status.Trim(), ignoreCase: true))
                .Distinct()
                .ToArray();
            rows = rows.Where(x => statuses.Contains(x.Status));
        }

        if (query.AdviserIds.Count > 0)
        {
            var adviserIds = Normalize(query.AdviserIds);
            rows = rows.Where(x => adviserIds.Contains(x.Slot.AdviserId));
        }

        if (query.ClientRefs.Count > 0)
        {
            var clientRefs = Normalize(query.ClientRefs);
            rows = rows.Where(x => clientRefs.Contains(x.UserId));
        }

        if (query.LocationRefs.Count > 0)
        {
            var locationRefs = Normalize(query.LocationRefs);
            rows = rows.Where(x => locationRefs.Contains(x.Slot.LocationRef!) || locationRefs.Contains(x.Slot.Transaction.LocationRef!));
        }

        if (query.MeetingTypes.Count > 0)
        {
            var meetingTypes = Normalize(query.MeetingTypes);
            rows = rows.Where(x => meetingTypes.Contains(x.Slot.Transaction.MeetingType!));
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

    private static string[] Normalize(IReadOnlyList<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
