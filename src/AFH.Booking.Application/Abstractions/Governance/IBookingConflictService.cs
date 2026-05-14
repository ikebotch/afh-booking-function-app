namespace AFH.Booking.Application.Abstractions.Governance;

public interface IBookingConflictService
{
    Task<BookingConflictCheckResult> EvaluateConfirmationConflictsAsync(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        string calendarUserId,
        CancellationToken ct);
}
