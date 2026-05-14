using System;
using System.Collections.Generic;
using System.Text;

namespace AFH.Booking.Application.Holds
{
    public interface IBookingHoldService
    {
        Task<Result<BookingHold>> CreateOrReplaceAsync(
            BookingContext context,
            DateTime utcNow,
            CancellationToken ct);
    }
}
