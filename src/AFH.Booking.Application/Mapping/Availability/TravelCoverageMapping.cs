using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Mapping.Availability;

public static class TravelCoverageMapping
{
    private const int DefaultCompanyBufferMinutes = 30;

    public static TravelMatrixResult ToTravelMatrixResult(
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
                            CompanyBufferMinutes = DefaultCompanyBufferMinutes,
                            PreMeetingBufferMinutes = (outcome.Route?.TravelTimeMinutes ?? 0) + DefaultCompanyBufferMinutes,
                            PostMeetingBufferMinutes = DefaultCompanyBufferMinutes,
                            MaxTravelTimeMinutes = outcome.Coverage?.MaxTravelTimeMinutes ?? profile.MaxTravelTimeMinutes ?? 0
                        },
                        CompanyBufferMinutes = DefaultCompanyBufferMinutes,
                        TravelSnapshot = new TravelSnapshotResult
                        {
                            SourceLocationRef = profile.AdviserId,
                            SourcePostcode = profile.HomePostcode,
                            SourceLatitude = outcome.Coordinates?.Latitude,
                            SourceLongitude = outcome.Coordinates?.Longitude,
                            DestinationLocationRef = null,
                            DestinationPostcode = clientDestination.Postcode,
                            DestinationLatitude = coverage.SourceCoordinates?.Latitude,
                            DestinationLongitude = coverage.SourceCoordinates?.Longitude,
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
}
