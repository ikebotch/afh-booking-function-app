using System;
using System.Collections.Generic;
using System.Text;

namespace AFH.Booking.Application.Holds
{
    public sealed record BookingContext(
      BookingSlot Slot,
      BookingTransaction Transaction,
      string CalendarUserId);

    public sealed class Unit
    {
        public static Unit Value { get; } = new();
        private Unit() { }
    }
}
