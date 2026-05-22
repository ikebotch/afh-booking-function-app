using AFH.Booking.Application.Models.Calendar;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Mapping.Availability;

public static class AvailabilityMapping
{
    public static AdviserSlotsDto ToDto(
        this AdviserProjectionItem adviser,
        string slotId,
        DateTime startUtc,
        DateTime endUtc,
        int score,
        LocationCandidate? travelCandidate,
        bool travelNotRequested,
        bool isRemote)
    {
        // Decide travel fields up-front (so we can use init-only properties)
        string? travelStatus = null;
        int? travelMinutes = null;
        int? companyBufferMinutes = null;
        decimal? distanceMiles = null;

        if (!isRemote)
        {
            travelStatus =
                travelNotRequested ? "NotRequested"
                : travelCandidate is null ? "Unavailable"
                : "Ok";

            travelMinutes = travelCandidate?.TravelMinutes;
            companyBufferMinutes = travelCandidate?.CompanyBufferMinutes;
            distanceMiles = travelCandidate?.DistanceMiles;
        }

        var slot = new SlotDto
        {
            SlotId = slotId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Rating = score,

            // these are safe even if init-only
            TravelMinutes = travelMinutes,
            CompanyBufferMinutes = companyBufferMinutes,
            DistanceMiles = distanceMiles,
            TravelStatus = travelStatus
        };

        return new AdviserSlotsDto
        {
            Id = adviser.AdviserId,
            Name = adviser?.Name ?? string.Empty,
            //Email = adviser?.Email ?? string.Empty,
            Slots = new List<SlotDto> { slot }
        };
    }

    public static TravelMatrixRequest ToTravelMatrixRequest(
        this GetAvailabilityQuery q,
        string correlationId,
        LocationAddress destination,
        IEnumerable<string> freeAdviserIds)
    {
        return new TravelMatrixRequest
        {
            RequestId = correlationId ?? $"BK-{Guid.NewGuid():N}",
            Meeting = new TravelMatrixMeeting
            {
                RequestedStartUtc = DateTime.SpecifyKind(q.PreferredStart, DateTimeKind.Utc),
                DurationMinutes = (int)Math.Round(q.Duration),
                SearchHorizonMinutes = q.SearchHorizonMinutes ?? 60,
            },
            Destination = new TravelMatrixDestination
            {
                Address = destination
            },
            Filters = new LocationFilters
            {
                MaxCandidates = q.MaxCandidates ?? 100,
                PreferredAdviserIds = q.PreferredAdviserIds.ToList(),
                ExcludeAdviserIds = q.ExcludeAdviserIds.ToList(),
                Regions = q.Regions,
                RequiredSkills = q.RequiredSkills.ToList(),
                AdviserIds = freeAdviserIds.ToList(),
            }
        };
    }
}
