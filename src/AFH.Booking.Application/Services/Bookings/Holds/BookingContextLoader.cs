
using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Models.Calendar;
using AFH.Booking.Application.Services.AdviserProjection;
using AFH.Booking.Domain.Bookings.Commands;

public sealed class BookingContextLoader : IBookingContextLoader
{
    private readonly IBookingSlotRepository _slotRepo;
    private readonly IBookingTransactionRepository _txRepo;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IAvailabilityRulesService _availabilityRules;
    private readonly IClock _clock;

    public BookingContextLoader(
        IBookingSlotRepository slotRepo,
        IBookingTransactionRepository txRepo,
        IAdviserProfileProjectionRepository profiles,
        IAvailabilityRulesService availabilityRules,
        IClock clock)
    {
        _slotRepo = slotRepo;
        _txRepo = txRepo;
        _profiles = profiles;
        _availabilityRules = availabilityRules;
        _clock = clock;
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

        var ruleValidation = await ValidateSelectedSlotRulesAsync(slot, transaction, ct);
        if (!ruleValidation.IsSuccess)
            return Result<BookingContext>.Fail(
                ruleValidation.StatusCode,
                ruleValidation.ErrorMessage,
                ruleValidation.ErrorCode);

        var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(
            slot.AdviserId,
            ct);

        return Result<BookingContext>.Ok(new BookingContext(
            slot,
            transaction,
            calendarUserId));
    }

    private async Task<Result> ValidateSelectedSlotRulesAsync(
        BookingSlot slot,
        BookingTransaction transaction,
        CancellationToken ct)
    {
        var profile = await _profiles.GetAsync(slot.AdviserId, ct);
        if (profile is null || !profile.IsActive)
        {
            return Result.Fail(
                System.Net.HttpStatusCode.Conflict,
                "The selected slot is no longer available because the adviser is not eligible.",
                Errors.SlotNoLongerAvailable);
        }

        var evaluation = await _availabilityRules.EvaluateAsync(
            new AdviserProjectionItem
            {
                AdviserId = profile.AdviserId,
                Name = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.AdviserId : profile.DisplayName,
                Email = string.IsNullOrWhiteSpace(profile.MailboxUserId) ? profile.AdviserId : profile.MailboxUserId,
                Region = profile.Region,
                HomePostcode = profile.HomePostcode
            },
            slot.StartUtc,
            slot.EndUtc,
            transaction.Duration.TotalMinutes,
            _clock.UtcNow,
            ct);

        return evaluation.IsAllowed
            ? Result.Ok()
            : Result.Fail(
                System.Net.HttpStatusCode.Conflict,
                $"The selected slot is no longer available because it fails adviser availability rules: {evaluation.RejectionReason}.",
                Errors.SlotNoLongerAvailable);
    }
}
