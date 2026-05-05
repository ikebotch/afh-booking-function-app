using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangementOrchestrator : IRearrangementOrchestrator
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ICreateBookingHandler _create;
    private readonly IConfirmBookingHandler _confirm;
    private readonly ICancellationOrchestrator _cancel;
    private readonly INotificationService _notifications;
    private readonly IDownstreamUpdateService _downstreamUpdates;
    private readonly ILifecycleAuditService _audit;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public RearrangementOrchestrator(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICreateBookingHandler create,
        IConfirmBookingHandler confirm,
        ICancellationOrchestrator cancel,
        INotificationService notifications,
        IDownstreamUpdateService downstreamUpdates,
        ILifecycleAuditService audit,
        IUnitOfWork uow,
        IClock clock)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _create = create;
        _confirm = confirm;
        _cancel = cancel;
        _notifications = notifications;
        _downstreamUpdates = downstreamUpdates;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<RearrangeBookingResponse>> RearrangeAsync(RearrangeBookingCommand cmd, CancellationToken ct)
    {
        var validation = BookingChangeValidation.Validate(cmd);
        if (!validation.IsSuccess)
            return Result<RearrangeBookingResponse>.Fail(validation.StatusCode, validation.ErrorMessage!, validation.ErrorCode);

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

        var before = new
        {
            previousBookingId = oldHold.Id,
            previousSlotId = oldSlot.Id,
            previousAdviserId = oldSlot.AdviserId,
            previousStartUtc = oldSlot.StartUtc,
            transactionId = tx.Id
        };

        var holdResult = await _create.HandleAsync(new CreateHoldCommand
        {
            SlotId = cmd.NewSlotId.Trim(),
            TransactionRef = tx.TransactionRef
        }, ct);

        if (!holdResult.IsSuccess || holdResult.Value is null)
            return Result<RearrangeBookingResponse>.Fail(holdResult.StatusCode, holdResult.ErrorMessage ?? "Unable to create hold for new slot.", holdResult.ErrorCode);

        var confirmResult = await _confirm.HandleAsync(new ConfirmBookingCommand
        {
            HoldId = holdResult.Value.BookingId,
            Notes = "Rearranged"
        }, ct);

        if (!confirmResult.IsSuccess || confirmResult.Value is null)
            return Result<RearrangeBookingResponse>.Fail(confirmResult.StatusCode, confirmResult.ErrorMessage ?? "Unable to confirm new booking.", confirmResult.ErrorCode);

        var cancelResult = await _cancel.CancelAsync(new CancelBookingCommand
        {
            BookingId = oldHold.Id,
            RequestedBy = cmd.RequestedBy,
            ReasonCode = string.IsNullOrWhiteSpace(cmd.ReasonCode) ? "Rearranged" : cmd.ReasonCode,
            ReasonDetail = cmd.ReasonDetail,
            Reason = BuildReason(cmd),
            CorrelationId = cmd.CorrelationId
        }, sendClientNotification: false, ct);

        if (!cancelResult.IsSuccess)
            return Result<RearrangeBookingResponse>.Fail(cancelResult.StatusCode, cancelResult.ErrorMessage ?? "Unable to cancel previous booking.", cancelResult.ErrorCode);

        var newHold = await _holds.GetAsync(holdResult.Value.BookingId, ct);
        if (newHold is null)
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.Conflict, "New booking hold was not found after confirmation.", Errors.Conflict);

        var newSlot = await _slots.GetAsync(newHold.SlotId, ct);
        if (newSlot is null)
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.Conflict, "New slot was not found after confirmation.", Errors.Conflict);

        var eventId = await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: newHold.Id,
            TransactionId: tx.Id,
            EventType: LifecycleEventTypes.ReArranged,
            ActorType: string.IsNullOrWhiteSpace(cmd.RequestedBy) ? LifecycleActors.Unknown : cmd.RequestedBy,
            ActorId: cmd.ActorId,
            ReasonCode: cmd.ReasonCode,
            ReasonNotes: cmd.ReasonDetail,
            Before: before,
            After: new
            {
                previousBookingId = oldHold.Id,
                newBookingId = newHold.Id,
                newSlotId = newSlot.Id,
                newAdviserId = newSlot.AdviserId,
                newStartUtc = newSlot.StartUtc
            },
            OccurredUtc: _clock.UtcNow,
            CorrelationId: cmd.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: oldHold.Id,
            PreviousState: LifecycleStates.Booked,
            NewState: LifecycleStates.Rearranged), ct);

        var now = _clock.UtcNow;
        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(eventId, LifecycleStepNames.Outlook, 1, LifecycleStepStatuses.Succeeded, now, now, null, null, cmd.CorrelationId), ct);
        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(eventId, LifecycleStepNames.SqlAudit, 2, LifecycleStepStatuses.Succeeded, now, now, null, null, cmd.CorrelationId), ct);
        await _uow.SaveChangesAsync(ct);

        var notificationSummary = BuildNotificationSummary(oldSlot, newSlot);
        var notificationStartedUtc = _clock.UtcNow;
        var notificationStatus = LifecycleStepStatuses.Succeeded;
        string? notificationErrorCode = null;
        string? notificationErrorDetails = null;

        try
        {
            await _notifications.SendBookingNotificationAsync(
                new NotificationDispatchRequest(
                    newHold.Id,
                    LifecycleEventTypes.ReArranged,
                    AppendReason(notificationSummary, cmd),
                    true,
                    true,
                    eventId,
                    cmd.CorrelationId),
                ct);
        }
        catch (Exception ex)
        {
            notificationStatus = LifecycleStepStatuses.Failed;
            notificationErrorCode = LifecycleErrorCodes.NotificationFailed;
            notificationErrorDetails = ex.Message;
        }

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.Notifications,
            3,
            notificationStatus,
            notificationStartedUtc,
            _clock.UtcNow,
            notificationErrorCode,
            notificationErrorDetails,
            cmd.CorrelationId), ct);
        await _uow.SaveChangesAsync(ct);

        await _downstreamUpdates.PublishBookingChangeAsync(
            bookingId: newHold.Id,
            changeType: "Rearrange",
            transactionRef: tx.TransactionRef,
            payloadJson: JsonSerializer.Serialize(new
            {
                previousBookingId = oldHold.Id,
                newBookingId = newHold.Id,
                previousSlotId = oldSlot.Id,
                newSlotId = newSlot.Id,
                requestedBy = cmd.RequestedBy,
                reasonCode = cmd.ReasonCode,
                reasonDetail = cmd.ReasonDetail,
                lifecycleEventId = eventId
            }),
            ct: ct);

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
            NotificationSummary = notificationSummary
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
            return $"Your meeting has been rearranged from {oldSlot.StartUtc:yyyy-MM-dd HH:mm} with {oldSlot.AdviserName} to {newSlot.StartUtc:yyyy-MM-dd HH:mm} with {newSlot.AdviserName}.";

        if (adviserChanged)
            return $"Your meeting adviser has changed from {oldSlot.AdviserName} to {newSlot.AdviserName}.";

        return $"Your meeting time has changed from {oldSlot.StartUtc:yyyy-MM-dd HH:mm} to {newSlot.StartUtc:yyyy-MM-dd HH:mm}.";
    }

    private static string AppendReason(string summary, RearrangeBookingCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.ReasonCode))
            return summary;

        var detail = string.IsNullOrWhiteSpace(cmd.ReasonDetail) ? string.Empty : $" - {cmd.ReasonDetail.Trim()}";
        return $"{summary} Reason: {cmd.ReasonCode}{detail}.";
    }
}
