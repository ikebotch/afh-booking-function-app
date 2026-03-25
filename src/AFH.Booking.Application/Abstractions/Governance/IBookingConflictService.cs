using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Abstractions.Governance;

public interface IBookingConflictService
{
    Task<BookingConflictCheckResult> EvaluateConfirmationConflictsAsync(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        CancellationToken ct);
}
