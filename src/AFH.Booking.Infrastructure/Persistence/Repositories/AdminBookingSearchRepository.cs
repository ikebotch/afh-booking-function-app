using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Bookings.Queries;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class AdminBookingSearchRepository : IAdminBookingSearchRepository
{
    private readonly BookingDbContext _db;
    private readonly IClientDirectory _clients;
    private readonly ILogger<AdminBookingSearchRepository> _logger;

    public AdminBookingSearchRepository(BookingDbContext db)
        : this(db, NullClientDirectory.Instance, Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminBookingSearchRepository>.Instance)
    {
    }

    public AdminBookingSearchRepository(
        BookingDbContext db,
        IClientDirectory clients,
        ILogger<AdminBookingSearchRepository> logger)
    {
        _db = db;
        _clients = clients;
        _logger = logger;
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
                BookingReference = x.Slot.Transaction.BookingReference ?? x.Reference,
                TransactionId = x.Slot.TransactionId,
                TransactionRef = x.Slot.Transaction.TransactionRef,
                ClientRef = x.UserId,
                ClientName = x.Slot.Transaction.ClientName,
                ClientEmail = x.Slot.Transaction.ClientEmail,
                ClientAddressLine1 = x.Slot.Transaction.ClientAddressLine1,
                ClientAddressLine2 = x.Slot.Transaction.ClientAddressLine2,
                ClientTown = x.Slot.Transaction.ClientTown,
                ClientCounty = x.Slot.Transaction.ClientCounty,
                ClientPostcode = x.Slot.Transaction.ClientPostcode,
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

        await EnrichClientsAsync(items, ct);

        return new AdminBookingSearchResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    private async Task EnrichClientsAsync(IReadOnlyList<AdminBookingSearchItem> items, CancellationToken ct)
    {
        var cache = new Dictionary<string, ClientDirectoryItem?>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ClientRef))
                continue;

            if (!cache.TryGetValue(item.ClientRef, out var client))
            {
                client = await TryGetClientAsync(item.ClientRef, ct);
                cache[item.ClientRef] = client;
            }

            if (client is null)
                continue;

            item.ClientName ??= BuildClientName(client);
            item.ClientEmail ??= client.Email;
            item.ClientAddressLine1 ??= client.StreetName1;
            item.ClientAddressLine2 ??= client.StreetName2;
            item.ClientTown ??= client.Town;
            item.ClientCounty ??= client.County;
            item.ClientPostcode ??= client.PostalCode;
        }
    }

    private async Task<ClientDirectoryItem?> TryGetClientAsync(string transactionRef, CancellationToken ct)
    {
        try
        {
            return await _clients.GetAsync(transactionRef, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Client lookup skipped while enriching booking search. TransactionRef={TransactionRef}", transactionRef);
            return null;
        }
    }

    private static string? BuildClientName(ClientDirectoryItem client)
    {
        var first = string.IsNullOrWhiteSpace(client.FirstName) ? null : client.FirstName.Trim();
        var last = string.IsNullOrWhiteSpace(client.LastName) ? null : client.LastName.Trim();
        var value = string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private IQueryable<BookingHoldModel> BuildQuery(SearchAdminBookingsQuery query)
    {
        var rows = _db.Holds
            .AsNoTracking()
            .Include(x => x.Slot)
            .ThenInclude(x => x.Transaction)
            .AsQueryable();

        if (!query.HasUnrestrictedAccess)
        {
            var scopedAdviserIds = Normalize(query.ScopedAdviserIds);
            var scopedRegions = Normalize(query.ScopedRegions);
            var scopedLocationRefs = Normalize(query.ScopedLocationRefs);

            if (scopedAdviserIds.Length == 0 && scopedRegions.Length == 0 && scopedLocationRefs.Length == 0)
                return rows.Where(_ => false);

            rows = rows.Where(x =>
                scopedAdviserIds.Contains(x.Slot.AdviserId)
                || scopedLocationRefs.Contains(x.Slot.LocationRef!)
                || scopedLocationRefs.Contains(x.Slot.Transaction.LocationRef!)
                || _db.AdviserProfileProjections.Any(profile =>
                    scopedRegions.Contains(profile.Region)
                    && profile.AdviserId == x.Slot.AdviserId));
        }

        if (query.BookingIds.Count > 0)
        {
            var bookingIds = Normalize(query.BookingIds);
            rows = rows.Where(x =>
                bookingIds.Contains(x.Id)
                || bookingIds.Contains(x.Reference!)
                || bookingIds.Contains(x.Slot.Transaction.BookingReference!));
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

    private sealed class NullClientDirectory : IClientDirectory
    {
        public static readonly NullClientDirectory Instance = new();

        public Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct)
            => Task.FromResult<ClientDirectoryItem?>(null);
    }
}
