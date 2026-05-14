using AFH.Booking.Application.Holds;
using System;
using System.Collections.Generic;
using System.Text;

namespace AFH.Booking.Application.Abstractions.Bookings.Holds
{
    public interface IBookingCalendarService
    {
        Task<Result<Unit>> CreateHoldEventAsync(
            BookingContext context,
            BookingHold hold,
            CancellationToken ct);
    }
}
