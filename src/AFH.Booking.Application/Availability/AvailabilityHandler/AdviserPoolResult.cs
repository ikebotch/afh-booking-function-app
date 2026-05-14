using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location;

namespace AFH.Booking.Application.Availability;

public sealed record AdviserPoolResult(
    IReadOnlyList<AdviserDirectoryItem> Advisers,
    IReadOnlyDictionary<string, LocationCandidate> TravelByAdviserId);
