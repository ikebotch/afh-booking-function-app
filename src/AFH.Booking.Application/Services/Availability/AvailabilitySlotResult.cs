using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Availability;

public sealed record AvailabilitySlotResult(
    string Key,
    string AdviserId,
    string Name,
    bool GoldStar,
    BookingSlot Slot);
