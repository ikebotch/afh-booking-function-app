
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings.Commands;

public sealed class BookingContextLoader : IBookingContextLoader
{
    private readonly IBookingSlotRepository _slotRepo;
    private readonly IBookingTransactionRepository _txRepo;
    private readonly IAdviserProfileProjectionRepository _profiles;

    public BookingContextLoader(
        IBookingSlotRepository slotRepo,
        IBookingTransactionRepository txRepo,
        IAdviserProfileProjectionRepository profiles)
    {
        _slotRepo = slotRepo;
        _txRepo = txRepo;
        _profiles = profiles;
    }

    public async Task<Result<BookingContext>> LoadAsync(
        CreateHoldCommand command,
        CancellationToken ct)
    {
        var slot = await _slotRepo.GetAsync(command.SlotId.Trim(), ct);
        if (slot is null)
        {
            return Result<BookingContext>.NotFound($"Slot '{command.SlotId}' not found.");
        }

        var transaction = await _txRepo.GetAsync(slot.TransactionId, ct);
        if (transaction is null)
        {
            return Result<BookingContext>.Fail(
                System.Net.HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' not found.",
                Errors.Conflict);
        }

        if (!string.IsNullOrWhiteSpace(command.TransactionRef) &&
            !string.Equals(
                command.TransactionRef.Trim(),
                transaction.TransactionRef,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<BookingContext>.Fail(
                System.Net.HttpStatusCode.Conflict,
                "slotId does not belong to supplied transactionRef.",
                Errors.Conflict);
        }

        var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(
            slot.AdviserId,
            ct);

        return Result<BookingContext>.Ok(new BookingContext(
            slot,
            transaction,
            calendarUserId));
    }
}