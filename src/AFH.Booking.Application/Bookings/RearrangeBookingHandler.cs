using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangeBookingHandler : IRearrangeBookingHandler
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ICreateBookingHandler _create;
    private readonly IConfirmBookingHandler _confirm;
    private readonly ICancelBookingHandler _cancel;

    public RearrangeBookingHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICreateBookingHandler create,
        IConfirmBookingHandler confirm,
        ICancelBookingHandler cancel)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _create = create;
        _confirm = confirm;
        _cancel = cancel;
    }

    public async Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        if (string.IsNullOrWhiteSpace(cmd.NewSlotId))
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.BadRequest, "newSlotId is required.", Errors.Validation);

        var oldHold = await _holds.GetAsync(cmd.BookingId.Trim(), ct);
        if (oldHold is null)
            return Result<RearrangeBookingResponse>.NotFound($"Booking '{cmd.BookingId}' was not found.");

        var oldSlot = await _slots.GetAsync(oldHold.SlotId, ct);
        if (oldSlot is null)
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.Conflict, $"Old slot '{oldHold.SlotId}' was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(oldSlot.TransactionId, ct);
        if (tx is null)
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{oldSlot.TransactionId}' was not found.", Errors.Conflict);

        var holdResult = await _create.HandleAsync(new CreateHoldCommand
        {
            SlotId = cmd.NewSlotId.Trim(),
            TransactionRef = tx.TransactionRef
        }, ct);

        if (!holdResult.IsSuccess || holdResult.Value is null)
        {
            return Result<RearrangeBookingResponse>.Fail(
                holdResult.StatusCode,
                holdResult.ErrorMessage ?? "Unable to create hold for new slot.",
                holdResult.ErrorCode);
        }

        var confirmResult = await _confirm.HandleAsync(new ConfirmBookingCommand
        {
            HoldId = holdResult.Value.BookingId,
            Notes = "Rearranged"
        }, ct);

        if (!confirmResult.IsSuccess || confirmResult.Value is null)
        {
            return Result<RearrangeBookingResponse>.Fail(
                confirmResult.StatusCode,
                confirmResult.ErrorMessage ?? "Unable to confirm new booking.",
                confirmResult.ErrorCode);
        }

        var reason = BuildReason(cmd);
        var cancelResult = await _cancel.HandleAsync(new CancelBookingCommand
        {
            BookingId = oldHold.Id,
            Reason = reason
        }, ct);

        if (!cancelResult.IsSuccess)
        {
            return Result<RearrangeBookingResponse>.Fail(
                cancelResult.StatusCode,
                cancelResult.ErrorMessage ?? "Unable to cancel previous booking.",
                cancelResult.ErrorCode);
        }

        var newHold = await _holds.GetAsync(holdResult.Value.BookingId, ct);
        if (newHold is null)
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.Conflict, "New booking hold was not found after confirmation.", Errors.Conflict);

        var newSlot = await _slots.GetAsync(newHold.SlotId, ct);
        if (newSlot is null)
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.Conflict, "New slot was not found after confirmation.", Errors.Conflict);

        return Result<RearrangeBookingResponse>.Ok(new RearrangeBookingResponse
        {
            PreviousBookingId = oldHold.Id,
            NewBookingId = newHold.Id,
            NewSlotId = newSlot.Id,
            PreviousAdviserId = oldSlot.AdviserId,
            PreviousAdviserName = oldSlot.AdviserName,
            PreviousStartUtc = oldSlot.StartUtc,
            PreviousEndUtc = oldSlot.EndUtc,
            NewAdviserId = newSlot.AdviserId,
            NewAdviserName = newSlot.AdviserName,
            NewStartUtc = newSlot.StartUtc,
            NewEndUtc = newSlot.EndUtc,
            NotificationSummary = BuildNotificationSummary(oldSlot, newSlot)
        });
    }

    private static string BuildReason(RearrangeBookingCommand cmd)
    {
        var requester = string.IsNullOrWhiteSpace(cmd.RequestedBy) ? "Unknown" : cmd.RequestedBy.Trim();
        var code = string.IsNullOrWhiteSpace(cmd.ReasonCode) ? "Rearrange" : cmd.ReasonCode.Trim();
        var detail = string.IsNullOrWhiteSpace(cmd.ReasonDetail) ? string.Empty : $": {cmd.ReasonDetail.Trim()}";
        return $"{requester} - {code}{detail}";
    }

    private static string BuildNotificationSummary(Domain.Transactions.BookingSlot oldSlot, Domain.Transactions.BookingSlot newSlot)
    {
        var adviserChanged = !string.Equals(oldSlot.AdviserId, newSlot.AdviserId, StringComparison.OrdinalIgnoreCase);
        var timeChanged = oldSlot.StartUtc != newSlot.StartUtc || oldSlot.EndUtc != newSlot.EndUtc;

        if (adviserChanged && timeChanged)
        {
            return $"Your meeting has been rearranged from {oldSlot.StartUtc:yyyy-MM-dd HH:mm} with {oldSlot.AdviserName} to {newSlot.StartUtc:yyyy-MM-dd HH:mm} with {newSlot.AdviserName}.";
        }

        if (adviserChanged)
        {
            return $"Your meeting adviser has changed from {oldSlot.AdviserName} to {newSlot.AdviserName}.";
        }

        return $"Your meeting time has changed from {oldSlot.StartUtc:yyyy-MM-dd HH:mm} to {newSlot.StartUtc:yyyy-MM-dd HH:mm}.";
    }
}
