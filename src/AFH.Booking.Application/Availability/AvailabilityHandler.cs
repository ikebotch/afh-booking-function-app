using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings.Mappings;
using AFH.Booking.Application.Bookings.Queries;
using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Score;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Common;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Bookings;

public sealed class AvailabilityHandler : IAvailabilityHandler
{
    private static readonly TimeSpan DefaultDayStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan DefaultDayEnd = TimeSpan.FromHours(17);

    private readonly ISlotScorer _scorer;
    private readonly ICalendarViewQueryHandler _calendarView;
    private readonly ITravelMatrixService _travelMatrix;
    private readonly IClientDirectory _clients;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IBookingTransactionRepository _txRepo;
    private readonly IBookingSlotRepository _slotRepo;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ILogger<AvailabilityHandler> _logger;

    public AvailabilityHandler(
        ISlotScorer scorer,
        ICalendarViewQueryHandler calendarView,
        ITravelMatrixService travelMatrix,
        IClientDirectory clients,
        IAdviserProfileProjectionRepository profiles,
        IBookingTransactionRepository txRepo,
        IBookingSlotRepository slotRepo,
        IUnitOfWork uow,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        ILogger<AvailabilityHandler> logger)
    {
        _scorer = scorer;
        _calendarView = calendarView;
        _travelMatrix = travelMatrix;
        _clients = clients;
        _profiles = profiles;
        _txRepo = txRepo;
        _slotRepo = slotRepo;
        _uow = uow;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _logger = logger;
    }

    public async Task<Result<GetAvailabilityResponse>> HandleAsync(GetAvailabilityQuery q, CancellationToken ct)
    {
        if (!ValidateQuery(q, out var error))
            return error!;

        var utcNow = _clock.UtcNow;

        var prospectResult = await LoadProspectIfRequired(q, ct);
        if (prospectResult.Error is not null)
            return prospectResult.Error;

        var prospect = prospectResult.Value;

        var (slotStartsUtc, nextCursor) = BuildSlotStartTimesUtcPage(q);
        if (slotStartsUtc.Count == 0)
            return EmptyResult(nextCursor);

        var txResult = CreateTransaction(q, slotStartsUtc[0], utcNow);
        if (txResult.Error is not null)
            return txResult.Error;

        var tx = txResult.Value!;
        await _txRepo.AddAsync(tx, ct);

        var adviserPoolResult = await BuildAdviserPoolAsync(q, prospect, ct);
        if (adviserPoolResult.Error is not null)
            return adviserPoolResult.Error;

        var advisers = adviserPoolResult.Value.Advisers;
        if (advisers.Count == 0)
            return EmptyResult(nextCursor);

        var adviserSlots = await ProcessSlots(
            q,
            advisers,
            slotStartsUtc,
            tx,
            prospect,
            adviserPoolResult.Value.TravelByAdviserId,
            utcNow,
            ct);

        await _uow.SaveChangesAsync(ct);

        return BuildSuccessResponse(q, tx.Id, adviserSlots, nextCursor);
    }

    private bool ValidateQuery(GetAvailabilityQuery q, out Result<GetAvailabilityResponse>? errorResult)
    {
        errorResult = null;

        if (string.IsNullOrWhiteSpace(q.TransactionId) && string.IsNullOrWhiteSpace(q.ClientId))
        {
            errorResult = Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.BadRequest,
                "Either transactionId or clientId must be provided.",
                Errors.Validation);
            return false;
        }

        if (q.Duration <= 0)
        {
            errorResult = Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.BadRequest,
                "duration must be > 0.",
                Errors.Validation);
            return false;
        }

        if (q.PreferredStart == default)
        {
            errorResult = Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.BadRequest,
                "proposedStartUtc is required.",
                Errors.Validation);
            return false;
        }

        return true;
    }

    private async Task<(Domain.Client.ClientDirectoryItem? Value, Result<GetAvailabilityResponse>? Error)> LoadProspectIfRequired(
        GetAvailabilityQuery q,
        CancellationToken ct)
    {
        if (q.IsRemote)
            return (null, null);

        var leadKey = string.IsNullOrWhiteSpace(q.TransactionId) ? q.ClientId : q.TransactionId;
        if (string.IsNullOrWhiteSpace(leadKey))
        {
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.BadRequest,
                    "transactionId or clientId is required for in-person meetings.",
                    Errors.Validation));
        }

        Domain.Client.ClientDirectoryItem? prospect;
        try
        {
            prospect = await _clients.GetAsync(leadKey.Trim(), ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Leads directory call failed for lookup key {LeadKey}.", leadKey);
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.BadGateway,
                    "Leads service is unavailable. Please try again shortly.",
                    "LeadsServiceUnavailable"));
        }

        if (prospect is null)
        {
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.NotFound,
                    "Client/prospect not found in leads directory.",
                    Errors.NotFound));
        }

        return (prospect, null);
    }

    private async Task<(AdviserPoolResult Value, Result<GetAvailabilityResponse>? Error)> BuildAdviserPoolAsync(
        GetAvailabilityQuery q,
        Domain.Client.ClientDirectoryItem? prospect,
        CancellationToken ct)
    {
        if (q.IsRemote)
        {
            var activeProfiles = await _profiles.ListActiveAsync(ct);
            var profileById = activeProfiles.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);

            var preferredIds = q.PreferredAdviserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => !q.ExcludeAdviserIds.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            IEnumerable<AdviserProfileProjectionRecord> remoteProfiles = preferredIds.Count > 0
                ? preferredIds
                    .Select(id => profileById.TryGetValue(id, out var profile)
                        ? profile
                        : new AdviserProfileProjectionRecord
                        {
                            AdviserId = id,
                            DisplayName = id,
                            MailboxUserId = id,
                            IsActive = true
                        })
                : activeProfiles.Where(x => !q.ExcludeAdviserIds.Contains(x.AdviserId, StringComparer.OrdinalIgnoreCase));

            var remoteAdvisers = remoteProfiles
                .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
                .Select(x => new AdviserDirectoryItem
                {
                    AdviserId = x.AdviserId,
                    Name = string.IsNullOrWhiteSpace(x.DisplayName) ? x.AdviserId : x.DisplayName,
                    Email = string.IsNullOrWhiteSpace(x.MailboxUserId) ? x.AdviserId : x.MailboxUserId
                })
                .DistinctBy(x => x.AdviserId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remoteAdvisers.Count == 0)
                return (new AdviserPoolResult([], new Dictionary<string, LocationCandidate>(StringComparer.OrdinalIgnoreCase)), null);

            return (new AdviserPoolResult(remoteAdvisers, new Dictionary<string, LocationCandidate>(StringComparer.OrdinalIgnoreCase)), null);
        }

        var travel = await GetTravelIfRequired(q, prospect, q.PreferredAdviserIds, ct);
        if (travel is null || travel.Candidates.Count == 0)
            return (new AdviserPoolResult([], new Dictionary<string, LocationCandidate>(StringComparer.OrdinalIgnoreCase)), null);

        var advisers = travel.Candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.AdviserId))
            .Where(c => c.IsEligible)
            .Where(c => !q.ExcludeAdviserIds.Contains(c.AdviserId, StringComparer.OrdinalIgnoreCase))
            .Select(c => new AdviserDirectoryItem
            {
                AdviserId = c.AdviserId,
                Name = string.IsNullOrWhiteSpace(c.AdviserName) ? c.AdviserId : c.AdviserName,
                Email = string.IsNullOrWhiteSpace(c.MailboxUserId) ? c.AdviserId : c.MailboxUserId
            })
            .DistinctBy(x => x.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var travelByAdviserId = travel.Candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
            .GroupBy(x => x.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return (new AdviserPoolResult(advisers, travelByAdviserId), null);
    }

    private (BookingTransaction? Value, Result<GetAvailabilityResponse>? Error) CreateTransaction(
        GetAvailabilityQuery q,
        DateTime firstSlot,
        DateTime utcNow)
    {
        try
        {
            var tx = BookingTransaction.Create(
                transactionRef: q.TransactionId ?? q.ClientId!,
                proposedStartUtc: firstSlot,
                duration: TimeSpan.FromMinutes(q.Duration),
                timezone: _timeZoneProvider.DefaultTimeZoneId,
                isRemote: q.IsRemote,
                meetingType: q.MeetingType,
                locationRef: q.LocationRef,
                utcNow: utcNow,
                expiresUtc: utcNow.AddMinutes(10));

            return (tx, null);
        }
        catch (DomainException ex)
        {
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.BadRequest,
                    ex.Message,
                    Errors.Validation));
        }
    }

    private async Task<List<(string Key, string AdviserId, string Name, bool GoldStar, BookingSlot Slot)>> ProcessSlots(
        GetAvailabilityQuery q,
        IReadOnlyList<AdviserDirectoryItem> advisers,
        IReadOnlyList<DateTime> slotStarts,
        BookingTransaction tx,
        Domain.Client.ClientDirectoryItem? prospect,
        IReadOnlyDictionary<string, LocationCandidate> travelByAdviserId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var result = new List<(string, string, string, bool, BookingSlot)>();

        foreach (var start in slotStarts)
        {
            var end = start.AddMinutes(q.Duration);

            var travel = q.IsRemote
                ? null
                : CreateTravelResultForAdvisers(advisers, travelByAdviserId);
            var availabilityByAdviserId = await GetAvailabilityByAdviserIdAsync(
                advisers,
                start,
                end,
                travel?.Candidates,
                ct);

            foreach (var adviser in advisers)
            {
                var adviserId = adviser.Email;
                if (string.IsNullOrWhiteSpace(adviserId))
                    continue;

                var travelCandidate = travel?.Candidates
                    .FirstOrDefault(x => string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

                if (!availabilityByAdviserId.TryGetValue(adviserId, out var availability) ||
                    !IsWindowFree(availability, start, end))
                {
                    continue;
                }

                if (!q.IsRemote)
                {
                    if (travel is not null && travelCandidate is null)
                        continue;

                    var t = travelCandidate?.TravelMinutes ?? 0;
                    if (t <= 0)
                        continue;

                    var fitsTravel = IsWindowFree(
                        availability,
                        start.AddMinutes(-t),
                        end.AddMinutes(t));

                    if (!fitsTravel)
                        continue;
                }

                var score = _scorer.Score(new SlotScoringContext
                {
                    StartUtc = start,
                    EndUtc = end,
                    IsRemote = q.IsRemote,
                    TravelMinutes = travelCandidate?.TravelMinutes,
                    AdviserPreferred = q.PreferredAdviserIds.Contains(adviserId, StringComparer.OrdinalIgnoreCase)
                });

                var slot = BookingSlot.Create(
                    id: Guid.NewGuid().ToString("N"),
                    transactionId: tx.Id,
                    adviserId: adviserId,
                    adviserName: adviser.Name ?? adviserId,
                    startUtc: start,
                    endUtc: end,
                    score: score.Score,
                    scoreBreakdown: score.Breakdown,
                    travel: travelCandidate,
                    locationRef: q.LocationRef,
                    utcNow: utcNow);

                await _slotRepo.AddAsync(slot, ct);

                result.Add((adviserId + slot.AdviserName, adviserId, slot.AdviserName, travelCandidate?.GoldStar ?? false, slot));
            }
        }

        return result;
    }

    private async Task<Dictionary<string, CalendarViewDto>> GetAvailabilityByAdviserIdAsync(
        IReadOnlyList<AdviserDirectoryItem> advisers,
        DateTime start,
        DateTime end,
        IReadOnlyList<LocationCandidate>? travelCandidates,
        CancellationToken ct)
    {
        var travelWindowPadding = travelCandidates?
            .Select(x => Math.Max(0, x.TravelMinutes ?? 0))
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        var calResult = await _calendarView.HandleAsync(new CalendarViewQuery
        {
            AdviserList = advisers,
            StartUtc = start.AddMinutes(-travelWindowPadding),
            EndUtc = end.AddMinutes(travelWindowPadding),
            Timezone = _timeZoneProvider.DefaultTimeZoneId
        }, ct);

        if (!calResult.IsSuccess)
            return new Dictionary<string, CalendarViewDto>(StringComparer.OrdinalIgnoreCase);

        return calResult.Value?
            .Where(c => !string.IsNullOrWhiteSpace(c.AdviserId))
            .GroupBy(c => c.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, CalendarViewDto>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<TravelMatrixResult?> GetTravelIfRequired(
        GetAvailabilityQuery q,
        Domain.Client.ClientDirectoryItem? prospect,
        IEnumerable<string> adviserIds,
        CancellationToken ct)
    {
        if (q.IsRemote || prospect is null)
            return null;

        var destination = new LocationAddress
        {
            Line1 = prospect.StreetName1 ?? q.DestinationAddress?.Line1 ?? string.Empty,
            Town = prospect.Town ?? q.DestinationAddress?.Town ?? string.Empty,
            Postcode = prospect.PostalCode ?? q.DestinationAddress?.Postcode ?? string.Empty,
            Country = q.DestinationAddress?.Country ?? "UK"
        };

        if (string.IsNullOrWhiteSpace(destination.Line1) ||
            string.IsNullOrWhiteSpace(destination.Town) ||
            string.IsNullOrWhiteSpace(destination.Postcode))
        {
            _logger.LogWarning("Leads returned incomplete address for transaction/client lookup. Travel matrix call skipped.");
            return null;
        }

        var req = q.ToTravelMatrixRequest(
            q.TransactionId ?? q.ClientId!,
            destination,
            adviserIds);

        return await _travelMatrix.GetAsync(req, ct);
    }

    private Result<GetAvailabilityResponse> BuildSuccessResponse(
        GetAvailabilityQuery q,
        string transactionId,
        List<(string Key, string AdviserId, string Name, bool GoldStar, BookingSlot Slot)> slots,
        string? nextCursor)
    {
        var pageSize = q.Limit <= 0 ? 10 : q.Limit;
        var dayGroups = AvailabilityResponseMapping.ToDayGroups(slots, pageSize);

        return Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse
        {
            TransactionId = transactionId,
            Advisers = dayGroups,
            Paging = new PageResultDto<object>
            {
                NextCursor = nextCursor,
                PageSize = pageSize,
                ReturnedCount = dayGroups?.Count ?? 0
            }
        });
    }

    private static Result<GetAvailabilityResponse> EmptyResult(string? nextCursor)
    {
        return Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse
        {
            Advisers = new(),
            Paging = new PageResultDto<object>
            {
                NextCursor = nextCursor,
                PageSize = 0,
                ReturnedCount = 0
            }
        });
    }

    private static TravelMatrixResult CreateTravelResultForAdvisers(
        IReadOnlyList<AdviserDirectoryItem> advisers,
        IReadOnlyDictionary<string, LocationCandidate> travelByAdviserId)
    {
        return new TravelMatrixResult
        {
            Candidates = advisers
                .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
                .Select(x => travelByAdviserId.TryGetValue(x.AdviserId, out var candidate) ? candidate : null)
                .Where(x => x is not null)
                .Cast<LocationCandidate>()
                .ToList()
        };
    }

    private static bool IsWindowFree(CalendarViewDto availability, DateTime windowStartUtc, DateTime windowEndUtc)
    {
        if (availability.MailboxUnavailable)
            return false;

        windowStartUtc = DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc);
        windowEndUtc = DateTime.SpecifyKind(windowEndUtc, DateTimeKind.Utc);

        if (windowEndUtc <= windowStartUtc)
            return false;

        if (availability.Conflicts.Count == 0)
            return !availability.IsBusy;

        return availability.Conflicts.All(conflict =>
            conflict.EndUtc <= windowStartUtc || conflict.StartUtc >= windowEndUtc);
    }

    private static (IReadOnlyList<DateTime> Starts, string? NextCursor) BuildSlotStartTimesUtcPage(GetAvailabilityQuery q)
    {
        var duration = TimeSpan.FromMinutes(q.Duration);
        var take = q.Take <= 0 ? 10 : Math.Min(q.Take, 100);

        DateTime? cursor = null;
        if (!string.IsNullOrWhiteSpace(q.Cursor) &&
            DateTime.TryParse(q.Cursor, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var c))
        {
            cursor = DateTime.SpecifyKind(c, DateTimeKind.Utc);
        }

        static bool AfterCursor(DateTime candidate, DateTime? cur)
            => cur is null || candidate > cur.Value;

        var result = new List<DateTime>(take);

        var preferred = DateTime.SpecifyKind(q.PreferredStart, DateTimeKind.Utc);

        var start = preferred.TimeOfDay == TimeSpan.Zero
            ? preferred.Date.Add(DefaultDayStart)
            : preferred;

        var end = preferred.TimeOfDay == TimeSpan.Zero
            ? preferred.Date.Add(DefaultDayEnd)
            : preferred.Add(duration);

        for (var t = start; t.Add(duration) <= end; t = t.Add(duration))
        {
            if (!AfterCursor(t, cursor))
                continue;

            result.Add(t);

            if (result.Count == take)
                return (result, t.Add(duration).ToString("O"));
        }

        return (result, null);
    }

    private sealed record AdviserPoolResult(
        IReadOnlyList<AdviserDirectoryItem> Advisers,
        IReadOnlyDictionary<string, LocationCandidate> TravelByAdviserId);
}
