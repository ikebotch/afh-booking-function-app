using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Holds;

public sealed class BookingHoldService : IBookingHoldService
{
    private static readonly TimeSpan DefaultHoldWindow = TimeSpan.FromMinutes(3);

    private readonly IBookingHoldRepository _holdRepo;
    private readonly IBookingTransactionRepository _txRepo;
    private readonly ICalendarGateway _calendar;
    private readonly IUnitOfWork _uow;

    public BookingHoldService(
        IBookingHoldRepository holdRepo,
        IBookingTransactionRepository txRepo,
        ICalendarGateway calendar,
        IUnitOfWork uow)
    {
        _holdRepo = holdRepo;
        _txRepo = txRepo;
        _calendar = calendar;
        _uow = uow;
    }

    public async Task<Result<BookingHold>> CreateOrReplaceAsync(
        BookingContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        var slotHold = await _holdRepo.GetBySlotIdAsync(context.Slot.Id, ct);

        if (slotHold is not null)
        {
            if (!string.Equals(
                    slotHold.BookingId,
                    context.Transaction.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result<BookingHold>.Fail(
                    HttpStatusCode.Conflict,
                    "This slot is already on hold.",
                    Errors.Conflict);
            }

            var activeTransactionHolds =
                await _holdRepo.GetAllActiveByTransactionIdAsync(
                    context.Transaction.Id,
                    utcNow,
                    ct);

            foreach (var existingHold in activeTransactionHolds.Where(x => x.Id != slotHold.Id))
            {
                await CancelCalendarEventIfExistsAsync(existingHold, ct);

                existingHold.Cancel(
                    "Superseded by returning to previous slot.",
                    utcNow);

                await _holdRepo.UpdateAsync(existingHold, ct);
            }

            slotHold.Reopen(
                utcNow,
                DefaultHoldWindow,
                context.CalendarUserId);

            context.Transaction.ExtendExpiry(slotHold.ExpiresUtc);

            await _holdRepo.UpdateAsync(slotHold, ct);
            await _txRepo.UpdateAsync(context.Transaction, ct);
            await _uow.SaveChangesAsync(ct);

            return Result<BookingHold>.Ok(slotHold);
        }

        var transactionHolds =
            await _holdRepo.GetAllActiveByTransactionIdAsync(
                context.Transaction.Id,
                utcNow,
                ct);

        foreach (var existingHold in transactionHolds)
        {
            await CancelCalendarEventIfExistsAsync(existingHold, ct);

            existingHold.Cancel(
                "Superseded by new hold attempt.",
                utcNow);

            await _holdRepo.UpdateAsync(existingHold, ct);
        }

        var newHold = BookingHold.Create(
            slotId: context.Slot.Id,
            userId: context.CalendarUserId,
            utcNow: utcNow,
            holdDuration: DefaultHoldWindow);

        context.Transaction.ExtendExpiry(newHold.ExpiresUtc);

        await _holdRepo.AddAsync(newHold, ct);
        await _txRepo.UpdateAsync(context.Transaction, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<BookingHold>.Ok(newHold);
    }

    private async Task CancelCalendarEventIfExistsAsync(
        BookingHold hold,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
            return;

        try
        {
            await _calendar.CancelBookingEventAsync(
                hold.UserId,
                hold.CalendarProviderEventId,
                ct);
        }
        finally
        {
            hold.ClearCalendarEvent();
        }
    }
}