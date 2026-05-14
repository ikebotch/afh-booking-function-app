using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Availability;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilitySlotProcessor
{
    Task<IReadOnlyList<AvailabilitySlotResult>> ProcessAsync(
        GetAvailabilityQuery query,
        IReadOnlyList<AdviserDirectoryItem> advisers,
        IReadOnlyList<DateTime> slotStarts,
        BookingTransaction transaction,
        IReadOnlyDictionary<string, LocationCandidate> travelByAdviserId,
        DateTime utcNow,
        CancellationToken ct);
}
