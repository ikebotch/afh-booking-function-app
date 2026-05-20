using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Availability.Mappings;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Availability;

public sealed class AdviserPoolBuilder : IAdviserPoolBuilder
{
    private static readonly char[] SkillWhitespaceSeparators = [' ', '\t', '\r', '\n'];

    private readonly ILocationTravelCoverageClient _travelCoverageClient;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly ILogger<AdviserPoolBuilder> _logger;

    public AdviserPoolBuilder(
        ILocationTravelCoverageClient travelCoverageClient,
        IAdviserProfileProjectionRepository profiles,
        ILogger<AdviserPoolBuilder> logger)
    {
        _travelCoverageClient = travelCoverageClient;
        _profiles = profiles;
        _logger = logger;
    }

    public async Task<(AdviserPoolResult Value, Result<GetAvailabilityResponse>? Error)> BuildAsync(
        GetAvailabilityQuery query,
        Domain.Client.ClientDirectoryItem? prospect,
        CancellationToken ct)
    {
        var normalizedRequiredSkills = NormalizeSkills(query.RequiredSkills);

        if (query.IsRemote)
            return await BuildRemotePoolAsync(query, normalizedRequiredSkills, ct);

        return await BuildInPersonPoolAsync(query, prospect, normalizedRequiredSkills, ct);
    }

    private async Task<(AdviserPoolResult Value, Result<GetAvailabilityResponse>? Error)> BuildRemotePoolAsync(
        GetAvailabilityQuery query,
        IReadOnlyList<string> normalizedRequiredSkills,
        CancellationToken ct)
    {
        var activeProfiles = await _profiles.ListActiveAsync(ct);
        var profileById = activeProfiles.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);

        var preferredIds = query.PreferredAdviserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => !query.ExcludeAdviserIds.Contains(x, StringComparer.OrdinalIgnoreCase))
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
            : activeProfiles.Where(x => !query.ExcludeAdviserIds.Contains(x.AdviserId, StringComparer.OrdinalIgnoreCase));

        var remoteProfilesList = remoteProfiles.ToList();
        var filteredRemoteProfiles = remoteProfilesList
            .Where(x => HasAllRequiredSkills(x.Skills, normalizedRequiredSkills))
            .ToList();

        _logger.LogInformation(
            "Booking availability adviser pool built. IsRemote={IsRemote} TransactionId={TransactionId} RequiredSkillsCount={RequiredSkillsCount} RequiredSkills={RequiredSkills} PreFilterAdviserCount={PreFilterAdviserCount} PostSkillFilterAdviserCount={PostSkillFilterAdviserCount}",
            true,
            query.TransactionId ?? query.ClientId,
            normalizedRequiredSkills.Count,
            normalizedRequiredSkills,
            remoteProfilesList.Count,
            filteredRemoteProfiles.Count);

        var remoteAdvisers = filteredRemoteProfiles
            .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
            .Select(x => new AdviserDirectoryItem
            {
                AdviserId = x.AdviserId,
                Name = string.IsNullOrWhiteSpace(x.DisplayName) ? x.AdviserId : x.DisplayName,
                Email = string.IsNullOrWhiteSpace(x.MailboxUserId) ? x.AdviserId : x.MailboxUserId,
                Region = x.Region,
                HomePostcode = x.HomePostcode
            })
            .DistinctBy(x => x.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (new AdviserPoolResult(
            remoteAdvisers,
            new Dictionary<string, LocationCandidate>(StringComparer.OrdinalIgnoreCase)), null);
    }

    private async Task<(AdviserPoolResult Value, Result<GetAvailabilityResponse>? Error)> BuildInPersonPoolAsync(
        GetAvailabilityQuery query,
        Domain.Client.ClientDirectoryItem? prospect,
        IReadOnlyList<string> normalizedRequiredSkills,
        CancellationToken ct)
    {
        var travel = await GetTravelIfRequired(query, prospect, normalizedRequiredSkills, ct);
        if (travel is null || travel.Candidates.Count == 0)
            return (new AdviserPoolResult([], new Dictionary<string, LocationCandidate>(StringComparer.OrdinalIgnoreCase)), null);

        var advisers = travel.Candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.AdviserId))
            .Where(c => c.IsEligible)
            .Where(c => !query.ExcludeAdviserIds.Contains(c.AdviserId, StringComparer.OrdinalIgnoreCase))
            .Select(c => new AdviserDirectoryItem
            {
                AdviserId = c.AdviserId,
                Name = string.IsNullOrWhiteSpace(c.AdviserName) ? c.AdviserId : c.AdviserName,
                Email = string.IsNullOrWhiteSpace(c.MailboxUserId) ? c.AdviserId : c.MailboxUserId,
                HomePostcode = c.TravelSnapshot?.SourcePostcode
            })
            .DistinctBy(x => x.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var travelByAdviserId = travel.Candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
            .GroupBy(x => x.AdviserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return (new AdviserPoolResult(advisers, travelByAdviserId), null);
    }

    private async Task<TravelMatrixResult?> GetTravelIfRequired(
        GetAvailabilityQuery query,
        Domain.Client.ClientDirectoryItem? prospect,
        IReadOnlyList<string> normalizedRequiredSkills,
        CancellationToken ct)
    {
        if (query.IsRemote || prospect is null)
            return null;

        var destination = new LocationAddress
        {
            Line1 = prospect.StreetName1 ?? query.DestinationAddress?.Line1 ?? string.Empty,
            Town = prospect.Town ?? query.DestinationAddress?.Town ?? string.Empty,
            Postcode = prospect.PostalCode ?? query.DestinationAddress?.Postcode ?? string.Empty,
            Country = query.DestinationAddress?.Country ?? "UK"
        };

        var requestKey = query.TransactionId ?? query.ClientId ?? "n/a";
        var profiles = await BuildCandidateProfilesAsync(query, normalizedRequiredSkills, ct);

        _logger.LogInformation(
            "Booking availability location request built. RequestKey={RequestKey} IsRemote={IsRemote} HasLine1={HasLine1} HasTown={HasTown} HasPostcode={HasPostcode} RequiredSkillsCount={RequiredSkillsCount} CandidateProfileCount={CandidateProfileCount}",
            requestKey,
            query.IsRemote,
            !string.IsNullOrWhiteSpace(destination.Line1),
            !string.IsNullOrWhiteSpace(destination.Town),
            !string.IsNullOrWhiteSpace(destination.Postcode),
            normalizedRequiredSkills.Count,
            profiles.Count);

        if (string.IsNullOrWhiteSpace(destination.Line1) ||
            string.IsNullOrWhiteSpace(destination.Town) ||
            string.IsNullOrWhiteSpace(destination.Postcode))
        {
            _logger.LogWarning(
                "Leads returned incomplete address for transaction/client lookup. Travel matrix call skipped. RequestKey={RequestKey}",
                requestKey);

            return null;
        }

        var request = new LocationTravelCoverageRequest
        {
            SourcePostcode = destination.Postcode,
            RequestedDepartureTime = DateTime.SpecifyKind(query.PreferredStart, DateTimeKind.Utc),
            TimingMode = LocationTravelTimingMode.TimeIndependent,
            AppointmentType = query.MeetingType,
            Channel = "BookingAvailability",
            CorrelationId = query.TransactionId ?? query.ClientId,
            RequestedBy = "booking-service",
            // TODO: Confirm travel directionality with the business before evolving this contract.
            // Travel time and distance are currently treated as scalar planning inputs for coverage,
            // calendar padding, and scoring. This one-source/many-destinations batching shape assumes
            // direction is effectively symmetric by using the client postcode as source and adviser
            // home postcodes as destinations. If adviser-to-client routing is required operationally,
            // evolve Location explicitly, likely via many-origins/one-destination, a general
            // origins/destinations matrix contract, or explicit travel direction metadata.
            Destinations = profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.HomePostcode))
                .Select(profile => new LocationTravelCoverageDestination
                {
                    CorrelationId = profile.AdviserId,
                    Postcode = profile.HomePostcode,
                    MaxTravelTimeMinutes = profile.MaxTravelTimeMinutes,
                    MaxDistanceMiles = profile.CoverageRadiusMiles
                })
                .ToList()
        };

        if (request.Destinations.Count == 0)
            return null;

        var coverage = await _travelCoverageClient.EvaluateAsync(request, ct);
        var result = MapTravelCoverageResult(destination, profiles, coverage);

        _logger.LogInformation(
            "Booking availability location response received. RequestKey={RequestKey} CandidateCount={CandidateCount} CandidateAdviserIds={CandidateAdviserIds}",
            requestKey,
            result.Candidates.Count,
            result.Candidates
                .Where(x => !string.IsNullOrWhiteSpace(x.AdviserId))
                .Select(x => x.AdviserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

        return result;
    }

    private async Task<List<AdviserProfileProjectionRecord>> BuildCandidateProfilesAsync(
        GetAvailabilityQuery query,
        IReadOnlyList<string> normalizedRequiredSkills,
        CancellationToken ct)
    {
        var activeProfiles = await _profiles.ListActiveAsync(ct);
        var profileById = activeProfiles.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);

        var preferredIds = query.PreferredAdviserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => !query.ExcludeAdviserIds.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = preferredIds.Count > 0
            ? preferredIds
                .Select(id => profileById.TryGetValue(id, out var profile) ? profile : null)
                .Where(profile => profile is not null)
                .Cast<AdviserProfileProjectionRecord>()
            : activeProfiles.Where(x => !query.ExcludeAdviserIds.Contains(x.AdviserId, StringComparer.OrdinalIgnoreCase));

        return profiles
            .Where(profile => profile.IsActive)
            .Where(profile => HasAllRequiredSkills(profile.Skills, normalizedRequiredSkills))
            .Where(profile => !string.IsNullOrWhiteSpace(profile.HomePostcode))
            .ToList();
    }

    private static TravelMatrixResult MapTravelCoverageResult(
        LocationAddress clientDestination,
        IReadOnlyList<AdviserProfileProjectionRecord> profiles,
        LocationTravelCoverageResult coverage)
    {
        var profileById = profiles.ToDictionary(x => x.AdviserId, StringComparer.OrdinalIgnoreCase);

        return new TravelMatrixResult
        {
            Candidates = coverage.Destinations
                .Where(outcome => outcome.Status == LocationTravelCoverageStatus.Succeeded)
                .Where(outcome => outcome.Coverage?.IsWithinCoverage == true)
                .Where(outcome => profileById.ContainsKey(outcome.CorrelationId))
                .Select(outcome =>
                {
                    var profile = profileById[outcome.CorrelationId];
                    return new LocationCandidate
                    {
                        AdviserId = profile.AdviserId,
                        AdviserName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.AdviserId : profile.DisplayName,
                        MailboxUserId = string.IsNullOrWhiteSpace(profile.MailboxUserId) ? profile.AdviserId : profile.MailboxUserId,
                        Region = profile.Region,
                        TravelMinutes = outcome.Route?.TravelTimeMinutes ?? 0,
                        DistanceMiles = outcome.Route is null ? null : Convert.ToDecimal(outcome.Route.TravelDistanceMiles),
                        Coverage = new CoverageInfo
                        {
                            WithinCoverage = outcome.Coverage?.IsWithinCoverage == true,
                            AnchorPostcode = profile.HomePostcode,
                            DistanceMiles = outcome.Route is null ? 0m : Convert.ToDecimal(outcome.Route.TravelDistanceMiles)
                        },
                        TravelToClient = new TravelToClient
                        {
                            EtaMinutes = outcome.Route?.TravelTimeMinutes,
                            DistanceMiles = outcome.Route is null ? null : Convert.ToDecimal(outcome.Route.TravelDistanceMiles),
                            Confidence = outcome.Route?.Confidence ?? "Low"
                        },
                        Buffers = new BufferInfo
                        {
                            MaxTravelTimeMinutes = outcome.Coverage?.MaxTravelTimeMinutes ?? profile.MaxTravelTimeMinutes ?? 0
                        },
                        TravelSnapshot = new TravelSnapshotResult
                        {
                            SourceLocationRef = profile.AdviserId,
                            SourcePostcode = profile.HomePostcode,
                            DestinationLocationRef = null,
                            DestinationPostcode = clientDestination.Postcode,
                            TravelMinutes = outcome.Route?.TravelTimeMinutes,
                            DistanceMiles = outcome.Route?.TravelDistanceMiles,
                            Provider = outcome.Route?.ResolutionSource.ToString(),
                            Confidence = outcome.Route?.Confidence,
                            CalculatedUtc = DateTime.UtcNow
                        }
                    };
                })
                .ToList()
        };
    }

    private static IReadOnlyList<string> NormalizeSkills(IEnumerable<string>? skills)
    {
        if (skills is null)
            return [];

        return skills
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeSkill)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasAllRequiredSkills(IReadOnlyList<string> adviserSkills, IReadOnlyList<string> requiredSkills)
    {
        if (requiredSkills.Count == 0)
            return true;

        if (adviserSkills is null || adviserSkills.Count == 0)
            return false;

        var normalizedAdviserSkills = adviserSkills
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeSkill)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredSkills.All(normalizedAdviserSkills.Contains);
    }

    private static string NormalizeSkill(string skill)
    {
        return string.Join(" ", skill
            .Trim()
            .Split(SkillWhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
