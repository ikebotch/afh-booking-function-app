using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface ISlotStartBuilder
{
    (IReadOnlyList<DateTime> Starts, string? NextCursor) BuildPage(GetAvailabilityQuery query);
}
