using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using System.Text.Json;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangementOrchestrator : IRearrangementOrchestrator
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ICreateBookingService _create;
    private readonly IConfirmBookingService _confirm;
    private readonly ICancellationOrchestrator _cancel;
    private readonly INotificationService _notifications;
    private readonly IBookingNotificationStep _notificationStep;
    private readonly IClientDirectory? _clients;
    private readonly IDownstreamUpdateService _downstreamUpdates;
    private readonly ILifecycleAuditService _audit;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public RearrangementOrchestrator(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICreateBookingService create,
        IConfirmBookingService confirm,
        ICancellationOrchestrator cancel,
        INotificationService notifications,
        IBookingNotificationStep notificationStep,
        IDownstreamUpdateService downstreamUpdates,
        ILifecycleAuditService audit,
        IUnitOfWork uow,
        IClock clock,
        IClientDirectory? clients = null)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _create = create;
        _confirm = confirm;
        _cancel = cancel;
        _notifications = notifications;
        _notificationStep = notificationStep;
        _clients = clients;
        _downstreamUpdates = downstreamUpdates;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<RearrangeBookingResponse>> RearrangeAsync(
        RearrangeBookingCommand cmd,
        CancellationToken ct)
    {
        var existingBookingResult = await LoadExistingBookingAsync(cmd, ct);
        if (!existingBookingResult.IsSuccess || existingBookingResult.Value is null)
            return FailLike<ExistingBookingContext, RearrangeBookingResponse>(existingBookingResult);

        var existingBooking = existingBookingResult.Value;
        var before = CreateBeforeSnapshot(existingBooking);

        var newBookingResult = await CreateAndConfirmNewBookingAsync(
            cmd,
            existingBooking.Transaction.TransactionRef,
            ct);
        if (!newBookingResult.IsSuccess || newBookingResult.Value is null)
            return FailLike<ConfirmedBookingContext, RearrangeBookingResponse>(newBookingResult);

        var cancelResult = await CancelPreviousBookingAsync(cmd, existingBooking.Hold.Id, ct);
        if (!cancelResult.IsSuccess)
            return FailLike<RearrangeBookingResponse>(cancelResult);

        var newBooking = newBookingResult.Value;
        var eventId = await RecordRearrangedLifecycleAsync(cmd, existingBooking, newBooking, before, ct);
        await _uow.SaveChangesAsync(ct);

        var notificationSummary = BuildNotificationSummary(existingBooking.Slot, newBooking.Slot);
        await RecordRearrangedNotificationStepAsync(
            cmd,
            existingBooking,
            newBooking,
            newBooking.Hold.Id,
            eventId,
            notificationSummary,
            ct);
        await _uow.SaveChangesAsync(ct);

        await PublishRearrangementUpdateAsync(cmd, existingBooking, newBooking, eventId, ct);

        return OkResponse(existingBooking, newBooking, notificationSummary);
    }

    private async Task<Result<ExistingBookingContext>> LoadExistingBookingAsync(
        RearrangeBookingCommand cmd,
        CancellationToken ct)
    {
        var validation = BookingChangeValidation.Validate(cmd);
        if (!validation.IsSuccess)
            return FailLike<ExistingBookingContext>(validation);

        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<ExistingBookingContext>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        if (string.IsNullOrWhiteSpace(cmd.NewSlotId))
            return Result<ExistingBookingContext>.Fail(HttpStatusCode.BadRequest, "newSlotId is required.", Errors.Validation);

        var hold = await _holds.GetAsync(cmd.BookingId.Trim(), ct);
        if (hold is null)
            return Result<ExistingBookingContext>.NotFound($"Booking '{cmd.BookingId}' was not found.");

        var actionable = BookingSelfServiceStatusRules.EnsureActionable(hold, "rearranged");
        if (!actionable.IsSuccess)
            return FailLike<ExistingBookingContext>(actionable);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<ExistingBookingContext>.Fail(HttpStatusCode.Conflict, $"Old slot '{hold.SlotId}' was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<ExistingBookingContext>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' was not found.", Errors.Conflict);

        return Result<ExistingBookingContext>.Ok(new ExistingBookingContext(hold, slot, tx));
    }

    private async Task<Result<ConfirmedBookingContext>> CreateAndConfirmNewBookingAsync(
        RearrangeBookingCommand cmd,
        string transactionRef,
        CancellationToken ct)
    {
        var holdResult = await _create.HandleAsync(new CreateHoldCommand
        {
            SlotId = cmd.NewSlotId.Trim(),
            TransactionRef = transactionRef
        }, ct);

        if (!holdResult.IsSuccess || holdResult.Value is null)
            return Result<ConfirmedBookingContext>.Fail(holdResult.StatusCode, holdResult.ErrorMessage ?? "Unable to create hold for new slot.", holdResult.ErrorCode);

        var confirmResult = await _confirm.HandleAsync(new ConfirmBookingCommand
        {
            HoldId = holdResult.Value.BookingId,
            Notes = "Rearranged"
        }, ct);

        if (!confirmResult.IsSuccess || confirmResult.Value is null)
            return Result<ConfirmedBookingContext>.Fail(confirmResult.StatusCode, confirmResult.ErrorMessage ?? "Unable to confirm new booking.", confirmResult.ErrorCode);

        var newHold = await _holds.GetAsync(holdResult.Value.BookingId, ct);
        if (newHold is null)
            return Result<ConfirmedBookingContext>.Fail(HttpStatusCode.Conflict, "New booking hold was not found after confirmation.", Errors.Conflict);

        var newSlot = await _slots.GetAsync(newHold.SlotId, ct);
        if (newSlot is null)
            return Result<ConfirmedBookingContext>.Fail(HttpStatusCode.Conflict, "New slot was not found after confirmation.", Errors.Conflict);

        return Result<ConfirmedBookingContext>.Ok(new ConfirmedBookingContext(newHold, newSlot));
    }

    private async Task<Result> CancelPreviousBookingAsync(
        RearrangeBookingCommand cmd,
        string previousBookingId,
        CancellationToken ct)
    {
        var cancelResult = await _cancel.CancelAsync(new CancelBookingCommand
        {
            BookingId = previousBookingId,
            RequestedBy = cmd.RequestedBy,
            ReasonCode = string.IsNullOrWhiteSpace(cmd.ReasonCode) ? "Rearranged" : cmd.ReasonCode,
            ReasonDetail = cmd.ReasonDetail,
            Reason = BuildReason(cmd),
            CorrelationId = cmd.CorrelationId
        }, sendClientNotification: false, ct);

        return cancelResult.IsSuccess
            ? Result.Ok()
            : Result.Fail(
                cancelResult.StatusCode,
                cancelResult.ErrorMessage ?? "Unable to cancel previous booking.",
                cancelResult.ErrorCode);
    }

    private async Task<string> RecordRearrangedLifecycleAsync(
        RearrangeBookingCommand cmd,
        ExistingBookingContext existingBooking,
        ConfirmedBookingContext newBooking,
        object before,
        CancellationToken ct)
    {
        var eventId = await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: newBooking.Hold.Id,
            TransactionId: existingBooking.Transaction.Id,
            EventType: LifecycleEventTypes.Rearranged,
            ActorType: string.IsNullOrWhiteSpace(cmd.RequestedBy) ? LifecycleActors.Unknown : cmd.RequestedBy,
            ActorId: cmd.ActorId,
            ReasonCode: cmd.ReasonCode,
            ReasonNotes: cmd.ReasonDetail,
            Before: before,
            After: new
            {
                previousBookingId = existingBooking.Hold.Id,
                newBookingId = newBooking.Hold.Id,
                newSlotId = newBooking.Slot.Id,
                newAdviserId = newBooking.Slot.AdviserId,
                newStartUtc = newBooking.Slot.StartUtc
            },
            OccurredUtc: _clock.UtcNow,
            CorrelationId: cmd.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: existingBooking.Hold.Id,
            PreviousState: LifecycleStates.Booked,
            NewState: LifecycleStates.Rearranged), ct);

        var now = _clock.UtcNow;
        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(eventId, LifecycleStepNames.Outlook, 1, LifecycleStepStatuses.Succeeded, now, now, null, null, cmd.CorrelationId), ct);
        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(eventId, LifecycleStepNames.SqlAudit, 2, LifecycleStepStatuses.Succeeded, now, now, null, null, cmd.CorrelationId), ct);

        return eventId;
    }

    private async Task RecordRearrangedNotificationStepAsync(
        RearrangeBookingCommand cmd,
        ExistingBookingContext existingBooking,
        ConfirmedBookingContext newBooking,
        string newBookingId,
        string eventId,
        string notificationSummary,
        CancellationToken ct)
    {
        var notificationStartedUtc = _clock.UtcNow;
        var notificationStatus = LifecycleStepStatuses.Succeeded;
        string? notificationErrorCode = null;
        string? notificationErrorDetails = null;

        try
        {
            await _notifications.SendBookingNotificationAsync(
                new NotificationDispatchRequest(
                    newBookingId,
                    LifecycleEventTypes.Rearranged,
                    AppendReason(notificationSummary, cmd),
                    true,
                    true,
                    eventId,
                    cmd.CorrelationId),
                ct);

            var client = _clients is null
                ? null
                : await _clients.GetAsync(existingBooking.Transaction.TransactionRef, ct);

            var result = await _notificationStep.ExecuteAsync(
                LifecycleEventTypes.Rearranged,
                newBookingId,
                ResolveNotificationActorType(cmd),
                BuildBookingRescheduledRecipients(client),
                BuildBookingRescheduledNotificationData(cmd, existingBooking, newBooking, eventId, notificationSummary),
                ct);

            notificationStatus = result.Status;
            notificationErrorCode = result.ErrorCode;
            notificationErrorDetails = result.ErrorDetails;
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
    }

    private static IReadOnlyList<NotificationRecipient> BuildBookingRescheduledRecipients(
        Domain.Client.ClientDirectoryItem? client)
    {
        if (client is null)
            return Array.Empty<NotificationRecipient>();

        var displayName = $"{client.FirstName} {client.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = null;

        return
        [
            new NotificationRecipient(
                BookingNotificationRecipientTypes.Client,
                displayName,
                client.Email,
                client.Phone)
        ];
    }

    private static IReadOnlyDictionary<string, string> BuildBookingRescheduledNotificationData(
        RearrangeBookingCommand cmd,
        ExistingBookingContext existingBooking,
        ConfirmedBookingContext newBooking,
        string eventId,
        string notificationSummary)
    {
        var note = AppendReason(notificationSummary, cmd);
        var template = BookingNotificationEmailTemplate.Build(
            eventType: "Rescheduled",
            clientDisplayName: null,
            adviserName: newBooking.Slot.AdviserName,
            startUtc: newBooking.Slot.StartUtc,
            endUtc: newBooking.Slot.EndUtc,
            timezoneId: existingBooking.Transaction.Timezone,
            isRemote: existingBooking.Transaction.IsRemote,
            customMessage: note);
        var lines = template.TextBody.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var dataByPrefix = lines
            .Select(line => line.Split(": ", 2, StringSplitOptions.None))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, string>
        {
            ["eventId"] = eventId,
            ["previousBookingId"] = existingBooking.Hold.Id,
            ["newBookingId"] = newBooking.Hold.Id,
            ["previousSlotId"] = existingBooking.Slot.Id,
            ["newSlotId"] = newBooking.Slot.Id,
            ["adviserName"] = newBooking.Slot.AdviserName,
            ["startUtc"] = newBooking.Slot.StartUtc.ToString("O"),
            ["endUtc"] = newBooking.Slot.EndUtc.ToString("O"),
            ["greetingName"] = "there",
            ["whenLine"] = dataByPrefix.GetValueOrDefault("When", string.Empty),
            ["locationLine"] = dataByPrefix.GetValueOrDefault("Meeting type", string.Empty),
            ["note"] = note,
            ["manageBookingLinks"] = string.Empty
        };
    }

    private async Task PublishRearrangementUpdateAsync(
        RearrangeBookingCommand cmd,
        ExistingBookingContext existingBooking,
        ConfirmedBookingContext newBooking,
        string eventId,
        CancellationToken ct)
    {
        await _downstreamUpdates.PublishBookingChangeAsync(
            bookingId: newBooking.Hold.Id,
            changeType: "Rearrange",
            transactionRef: existingBooking.Transaction.TransactionRef,
            payloadJson: JsonSerializer.Serialize(new
            {
                previousBookingId = existingBooking.Hold.Id,
                newBookingId = newBooking.Hold.Id,
                previousSlotId = existingBooking.Slot.Id,
                newSlotId = newBooking.Slot.Id,
                requestedBy = cmd.RequestedBy,
                reasonCode = cmd.ReasonCode,
                reasonDetail = cmd.ReasonDetail,
                lifecycleEventId = eventId
            }),
            ct: ct);
    }

    private static Result<RearrangeBookingResponse> OkResponse(
        ExistingBookingContext existingBooking,
        ConfirmedBookingContext newBooking,
        string notificationSummary)
    {
        return Result<RearrangeBookingResponse>.Ok(new RearrangeBookingResponse
        {
            PreviousBookingId = existingBooking.Hold.Id,
            NewBookingId = newBooking.Hold.Id,
            NewSlotId = newBooking.Slot.Id,
            PreviousAdviserId = existingBooking.Slot.AdviserId,
            PreviousAdviserName = existingBooking.Slot.AdviserName,
            PreviousStartUtc = existingBooking.Slot.StartUtc,
            PreviousEndUtc = existingBooking.Slot.EndUtc,
            NewAdviserId = newBooking.Slot.AdviserId,
            NewAdviserName = newBooking.Slot.AdviserName,
            NewStartUtc = newBooking.Slot.StartUtc,
            NewEndUtc = newBooking.Slot.EndUtc,
            NotificationSummary = notificationSummary
        });
    }

    private static object CreateBeforeSnapshot(ExistingBookingContext existingBooking)
    {
        return new
        {
            previousBookingId = existingBooking.Hold.Id,
            previousSlotId = existingBooking.Slot.Id,
            previousAdviserId = existingBooking.Slot.AdviserId,
            previousStartUtc = existingBooking.Slot.StartUtc,
            transactionId = existingBooking.Transaction.Id
        };
    }

    private static string BuildReason(RearrangeBookingCommand cmd)
    {
        var requester = string.IsNullOrWhiteSpace(cmd.RequestedBy) ? "Unknown" : cmd.RequestedBy.Trim();
        var code = string.IsNullOrWhiteSpace(cmd.ReasonCode) ? "Rearrange" : cmd.ReasonCode.Trim();
        var detail = string.IsNullOrWhiteSpace(cmd.ReasonDetail) ? string.Empty : $": {cmd.ReasonDetail.Trim()}";
        return $"{requester} - {code}{detail}";
    }

    private static string BuildNotificationSummary(BookingSlot oldSlot, BookingSlot newSlot)
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

    private static string ResolveNotificationActorType(RearrangeBookingCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.RequestedBy))
            return LifecycleActors.System;

        return cmd.RequestedBy.Trim();
    }

    private static Result<T> FailLike<T>(Result failure)
    {
        return Result<T>.Fail(
            failure.StatusCode,
            failure.ErrorMessage ?? "Request failed.",
            failure.ErrorCode);
    }

    private static Result<TTo> FailLike<TFrom, TTo>(Result<TFrom> failure)
    {
        return Result<TTo>.Fail(
            failure.StatusCode,
            failure.ErrorMessage ?? "Request failed.",
            failure.ErrorCode);
    }

    private sealed record ExistingBookingContext(
        BookingHold Hold,
        BookingSlot Slot,
        BookingTransaction Transaction);

    private sealed record ConfirmedBookingContext(
        BookingHold Hold,
        BookingSlot Slot);
}
