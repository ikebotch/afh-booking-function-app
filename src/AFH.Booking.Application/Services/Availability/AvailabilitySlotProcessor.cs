using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Models.Calendar;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings.Score;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Availability;

public sealed class AvailabilitySlotProcessor : IAvailabilitySlotProcessor
{
    private readonly ISlotScorer _scorer;
    private readonly ICalendarViewQueryService _calendarView;
    private readonly IBookingSlotRepository _slotRepo;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly IAvailabilityRulesService _availabilityRules;
    private readonly ILogger<AvailabilitySlotProcessor> _logger;

    public AvailabilitySlotProcessor(
        ISlotScorer scorer,
        ICalendarViewQueryService calendarView,
        IBookingSlotRepository slotRepo,
        ITimeZoneProvider timeZoneProvider,
        IAvailabilityRulesService availabilityRules,
        ILogger<AvailabilitySlotProcessor> logger)
    {
        _scorer = scorer;
        _calendarView = calendarView;
        _slotRepo = slotRepo;
        _timeZoneProvider = timeZoneProvider;
        _availabilityRules = availabilityRules;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailabilitySlotResult>> ProcessAsync(
        GetAvailabilityQuery query,
        IReadOnlyList<AdviserProjectionItem> advisers,
        IReadOnlyList<DateTime> slotStarts,
        BookingTransaction transaction,
        IReadOnlyDictionary<string, LocationCandidate> travelByAdviserId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var result = new List<AvailabilitySlotResult>();
        var requestId = query.TransactionId ?? query.ClientId;
        var calendarWindowPassCount = 0;
        var workingPatternFailCount = 0;
        var capacityFailCount = 0;
        var minimumDurationFailCount = 0;
        var missingTravelCandidateCount = 0;
        var nonPositiveTravelCount = 0;
        var travelFitPassCount = 0;
        var travelFitFailCount = 0;
        var candidateEvaluationCount = 0;

        var travel = query.IsRemote
            ? null
            : CreateTravelResultForAdvisers(advisers, travelByAdviserId);

        var availabilityByAdviserId = await GetAvailabilityByAdviserIdAsync(
            advisers,
            slotStarts,
            query.Duration,
            query.IsRemote,
            travel?.Candidates,
            ct);

        foreach (var start in slotStarts)
        {
            var end = start.AddMinutes(query.Duration);

            foreach (var adviser in advisers)
            {
                candidateEvaluationCount++;
                var adviserId = adviser.AdviserId;
                if (string.IsNullOrWhiteSpace(adviserId))
                    continue;

                IReadOnlyDictionary<string, int>? ruleAudit = null;

                // Keep in-person behaviour aligned with the working version:
                // travel + calendar decide in-person availability.
                if (query.IsRemote)
                {
                    var rules = await _availabilityRules.EvaluateAsync(
                        adviser,
                        start,
                        end,
                        query.Duration,
                        utcNow,
                        ct);

                    ruleAudit = rules.Audit;

                    if (!rules.IsAllowed)
                    {
                        if (!rules.WorkingPatternAllowed)
                            workingPatternFailCount++;

                        if (!rules.CapacityAllowed)
                            capacityFailCount++;

                        if (!rules.MinimumDurationAllowed)
                            minimumDurationFailCount++;

                        continue;
                    }
                }

                var travelCandidate = travel?.Candidates
                    .FirstOrDefault(x => string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase));

                if (!query.IsRemote)
                {
                    if (travel is not null && travelCandidate is null)
                    {
                        missingTravelCandidateCount++;
                        continue;
                    }
                }

                var holdWindow = CreateCandidateHoldWindow(start, end, query.IsRemote, travelCandidate);

                if (!availabilityByAdviserId.TryGetValue(adviserId, out var availability) ||
                    !IsWindowFree(availability, holdWindow.StartUtc, holdWindow.EndUtc))
                {
                    if (!query.IsRemote)
                        travelFitFailCount++;

                    continue;
                }

                calendarWindowPassCount++;

                if (!query.IsRemote)
                    travelFitPassCount++;

                var score = _scorer.Score(new SlotScoringContext
                {
                    StartUtc = start,
                    EndUtc = end,
                    IsRemote = query.IsRemote,
                    TravelMinutes = travelCandidate?.TravelMinutes,
                    AdviserPreferred = query.PreferredAdviserIds.Contains(adviserId, StringComparer.OrdinalIgnoreCase)
                });

                var scoreBreakdown = ruleAudit is null
                    ? score.Breakdown
                    : MergeAudit(score.Breakdown, ruleAudit);

                var slot = BookingSlot.Create(
                    id: Guid.NewGuid().ToString("N"),
                    transactionId: transaction.Id,
                    adviserId: adviserId,
                    adviserName: adviser.Name ?? adviserId,
                    startUtc: start,
                    endUtc: end,
                    score: score.Score,
                    scoreBreakdown: scoreBreakdown,
                    travel: travelCandidate,
                    locationRef: query.LocationRef,
                    utcNow: utcNow);

                AttachTravelSnapshot(slot, query, travelCandidate);

                await _slotRepo.AddAsync(slot, ct);

                result.Add(new AvailabilitySlotResult(
                    adviserId + slot.AdviserName,
                    adviserId,
                    slot.AdviserName,
                    travelCandidate?.GoldStar ?? false,
                    slot));
            }
        }

        started.Stop();
        _logger.LogInformation(
            "Booking availability slot filtering complete. IsRemote={IsRemote} TransactionId={TransactionId} AdviserPoolCount={AdviserPoolCount} SlotStartCount={SlotStartCount} CandidateCountBeforeFiltering={CandidateCountBeforeFiltering} CalendarProviderCallCount={CalendarProviderCallCount} CalendarWindowPassCount={CalendarWindowPassCount} MissingTravelCandidateCount={MissingTravelCandidateCount} NonPositiveTravelCount={NonPositiveTravelCount} TravelFitPassCount={TravelFitPassCount} TravelFitFailCount={TravelFitFailCount} CandidateCountAfterFiltering={CandidateCountAfterFiltering} DurationMs={DurationMs}",
            query.IsRemote,
            requestId,
            advisers.Count,
            slotStarts.Count,
            candidateEvaluationCount,
            1,
            calendarWindowPassCount,
            missingTravelCandidateCount,
            nonPositiveTravelCount,
            travelFitPassCount,
            travelFitFailCount,
            result.Count,
            started.ElapsedMilliseconds);

        if (workingPatternFailCount > 0 || capacityFailCount > 0 || minimumDurationFailCount > 0)
        {
            _logger.LogInformation(
                "Booking availability rule filtering complete. TransactionId={TransactionId} WorkingPatternFailCount={WorkingPatternFailCount} CapacityFailCount={CapacityFailCount} MinimumDurationFailCount={MinimumDurationFailCount}",
                requestId,
                workingPatternFailCount,
                capacityFailCount,
                minimumDurationFailCount);
        }

        return result;
    }

    private async Task<Dictionary<string, CalendarViewDto>> GetAvailabilityByAdviserIdAsync(
        IReadOnlyList<AdviserProjectionItem> advisers,
        IReadOnlyList<DateTime> slotStarts,
        double durationMinutes,
        bool isRemote,
        IReadOnlyList<LocationCandidate>? travelCandidates,
        CancellationToken ct)
    {
        var maxPreMeetingPadding = isRemote
            ? 0
            : travelCandidates?
                .Select(x => GetTravelMinutes(x) + GetCompanyBufferMinutes(x))
                .DefaultIfEmpty(0)
                .Max() ?? 0;

        var maxPostMeetingPadding = isRemote
            ? 0
            : travelCandidates?
                .Select(GetCompanyBufferMinutes)
                .DefaultIfEmpty(0)
                .Max() ?? 0;

        var minStart = slotStarts.Min();
        var maxEnd = slotStarts.Max().AddMinutes(durationMinutes);

        var calResult = await _calendarView.HandleAsync(new CalendarViewQuery
        {
            AdviserList = advisers,
            StartUtc = minStart.AddMinutes(-maxPreMeetingPadding),
            EndUtc = maxEnd.AddMinutes(maxPostMeetingPadding),
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

    private static TravelMatrixResult CreateTravelResultForAdvisers(
        IReadOnlyList<AdviserProjectionItem> advisers,
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

    private static (DateTime StartUtc, DateTime EndUtc) CreateCandidateHoldWindow(
        DateTime start,
        DateTime end,
        bool isRemote,
        LocationCandidate? travelCandidate)
    {
        if (isRemote)
            return (start, end);

        var travelMinutes = GetTravelMinutes(travelCandidate);
        var companyBufferMinutes = GetCompanyBufferMinutes(travelCandidate);

        return (
            start.AddMinutes(-(travelMinutes + companyBufferMinutes)),
            end.AddMinutes(companyBufferMinutes));
    }

    private static int GetTravelMinutes(LocationCandidate? travelCandidate)
        => Math.Max(0, travelCandidate?.TravelSnapshot?.TravelMinutes ?? travelCandidate?.TravelMinutes ?? 0);

    private static int GetCompanyBufferMinutes(LocationCandidate? travelCandidate)
        => Math.Max(0, travelCandidate?.CompanyBufferMinutes ?? travelCandidate?.Buffers?.CompanyBufferMinutes ?? 30);

    private static void AttachTravelSnapshot(
        BookingSlot slot,
        GetAvailabilityQuery query,
        LocationCandidate? travelCandidate)
    {
        if (query.IsRemote)
        {
            slot.AttachTravelSnapshot(
                travelMinutes: 0,
                distanceMiles: null,
                companyBufferMinutes: 0,
                sourceLocationRef: null,
                sourcePostcode: null,
                sourceLatitude: null,
                sourceLongitude: null,
                destinationLocationRef: query.LocationRef,
                destinationPostcode: query.DestinationAddress?.Postcode,
                destinationLatitude: null,
                destinationLongitude: null,
                provider: null,
                confidence: null,
                calculatedUtc: null);
            return;
        }

        var snapshot = travelCandidate?.TravelSnapshot;
        var companyBufferMinutes = travelCandidate?.CompanyBufferMinutes ?? travelCandidate?.Buffers?.CompanyBufferMinutes;

        slot.AttachTravelSnapshot(
            travelMinutes: snapshot?.TravelMinutes ?? travelCandidate?.TravelMinutes,
            distanceMiles: snapshot?.DistanceMiles ?? ConvertDistanceMiles(travelCandidate?.DistanceMiles),
            companyBufferMinutes: companyBufferMinutes,
            sourceLocationRef: snapshot?.SourceLocationRef ?? travelCandidate?.AdviserId,
            sourcePostcode: snapshot?.SourcePostcode,
            sourceLatitude: snapshot?.SourceLatitude,
            sourceLongitude: snapshot?.SourceLongitude,
            destinationLocationRef: snapshot?.DestinationLocationRef ?? query.LocationRef,
            destinationPostcode: snapshot?.DestinationPostcode ?? query.DestinationAddress?.Postcode,
            destinationLatitude: snapshot?.DestinationLatitude,
            destinationLongitude: snapshot?.DestinationLongitude,
            provider: snapshot?.Provider ?? "LocationService",
            confidence: snapshot?.Confidence ?? travelCandidate?.TravelToClient?.Confidence,
            calculatedUtc: snapshot?.CalculatedUtc);
    }

    private static double? ConvertDistanceMiles(decimal? distanceMiles)
        => distanceMiles.HasValue ? Convert.ToDouble(distanceMiles.Value) : null;

    private static IReadOnlyDictionary<string, int> MergeAudit(
        IReadOnlyDictionary<string, int>? scoreBreakdown,
        IReadOnlyDictionary<string, int> ruleAudit)
    {
        var merged = scoreBreakdown is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(scoreBreakdown, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in ruleAudit)
            merged[$"rule.{key}"] = value;

        return merged;
    }
}
