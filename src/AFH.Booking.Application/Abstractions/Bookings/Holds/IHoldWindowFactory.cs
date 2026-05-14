using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Holds;

public interface IHoldWindowFactory
{
    HoldWindows Create(
        BookingSlot slot,
        BookingTransaction transaction);
}