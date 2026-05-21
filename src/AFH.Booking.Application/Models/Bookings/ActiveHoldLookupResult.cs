namespace AFH.Booking.Application.Models.Bookings;

public sealed record ActiveHoldLookupResult(
    BookingHold? TransactionHold,
    BookingHold? SlotHold);
