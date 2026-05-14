using AFH.Booking.Domain.Bookings.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace AFH.Booking.Application.Holds
{
    public interface IBookingContextLoader
    {
        Task<Result<BookingContext>> LoadAsync(
            CreateHoldCommand command,
            CancellationToken ct);
    }
}
