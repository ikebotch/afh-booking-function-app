using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location;

namespace AFH.Booking.Application.Availability;

public sealed record AdviserPoolResult(
    IReadOnlyList<AdviserProjectionItem> Advisers,
    IReadOnlyDictionary<string, LocationCandidate> TravelByAdviserId);
