using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Services.Notifications;
using AFH.Booking.Domain.Bookings.Commands;
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
    private readonly IBookingWorkflowNotificationAdapter _notifications;
    private readonly IClientDirectory? _clients;
    private readonly IDownstreamUpdateService _downstreamUpdates;
    private readonly IBookingLifecycleRecorder _lifecycle;
    private readonly IBookingWorkflowIdempotencyGuard _idempotency;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public RearrangementOrchestrator(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICreateBookingService create,
        IConfirmBookingService confirm,
        ICancellationOrchestrator cancel,
        IBookingWorkflowNotificationAdapter notifications,
        IDownstreamUpdateService downstreamUpdates,
        IBookingLifecycleRecorder lifecycle,
        IBookingWorkflowIdempotencyGuard idempotency,
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
        _clients = clients;
        _downstreamUpdates = downstreamUpdates;
        _lifecycle = lifecycle;
        _idempotency = idempotency;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<RearrangeBookingResponse>> RearrangeAsync(
        RearrangeBookingCommand cmd,
        CancellationToken ct)
    {
        var validation = BookingChangeValidation.Validate(cmd);
        if (!validation.IsSuccess)
            return FailLike<RearrangeBookingResponse>(validation);

        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        if (string.IsNullOrWhiteSpace(cmd.NewSlotId))
            return Result<RearrangeBookingResponse>.Fail(HttpStatusCode.BadRequest, "newSlotId is required.", Errors.Validation);

        var workflowKey = BookingWorkflowIdempotencyKeys.Rearrangement(cmd.BookingId, cmd.NewSlotId, cmd.RequestedBy);
        var existing = await _idempotency.FindCompletedAsync(workflowKey, ct);
        if (existing is not null)
            return TryBuildIdempotentRearrangeResponse(existing);

        var existingBookingResult = await LoadExistingBookingAsync(cmd, ct);
        if (!existingBookingResult.IsSuccess || existingBookingResult.Value is null)
            return FailLike<ExistingBookingContext, RearrangeBookingResponse>(existingBookingResult);

        var existingBooking = existingBookingResult.Value;
        var before = CreateBeforeSnapshot(existingBooking);

        var selectedOptionResult = await ResolveSelectedOptionAsync(cmd, existingBooking, ct);
        if (!selectedOptionResult.IsSuccess || selectedOptionResult.Value is null)
            return FailLike<SelectedRearrangementOption, RearrangeBookingResponse>(selectedOptionResult);

        var newBookingResult = await CreateAndConfirmNewBookingAsync(
            cmd,
            selectedOptionResult.Value.Transaction.TransactionRef,
            ct);
        if (!newBookingResult.IsSuccess || newBookingResult.Value is null)
            return FailLike<ConfirmedBookingContext, RearrangeBookingResponse>(newBookingResult);

        var cancelResult = await CancelPreviousBookingAsync(cmd, existingBooking.Hold.Id, ct);
        if (!cancelResult.IsSuccess)
            return FailLike<RearrangeBookingResponse>(cancelResult);

        var newBooking = newBookingResult.Value;
        var notificationSummary = BuildNotificationSummary(existingBooking.Slot, newBooking.Slot);
        var eventId = await RecordRearrangedLifecycleAsync(cmd, existingBooking, newBooking, before, workflowKey, notificationSummary, ct);
        await _uow.SaveChangesAsync(ct);

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

    private async Task<Result<SelectedRearrangementOption>> ResolveSelectedOptionAsync(
        RearrangeBookingCommand cmd,
        ExistingBookingContext existingBooking,
        CancellationToken ct)
    {
        var selectedSlot = await _slots.GetAsync(cmd.NewSlotId.Trim(), ct);
        if (selectedSlot is null)
        {
            return Result<SelectedRearrangementOption>.Fail(
                HttpStatusCode.Conflict,
                $"Selected rearrangement slot '{cmd.NewSlotId}' is no longer available.",
                Errors.SlotNoLongerAvailable);
        }

        var optionTransaction = await _transactions.GetAsync(selectedSlot.TransactionId, ct);
        if (optionTransaction is null)
        {
            return Result<SelectedRearrangementOption>.Fail(
                HttpStatusCode.Conflict,
                $"Rearrangement option context for slot '{cmd.NewSlotId}' was not found.",
                Errors.SlotNoLongerAvailable);
        }

        if (optionTransaction.IsExpired(_clock.UtcNow))
        {
            return Result<SelectedRearrangementOption>.Fail(
                HttpStatusCode.Conflict,
                $"Rearrangement option for slot '{cmd.NewSlotId}' has expired.",
                Errors.SlotNoLongerAvailable);
        }

        if (!string.Equals(optionTransaction.TransactionRef, existingBooking.Transaction.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Result<SelectedRearrangementOption>.Fail(
                HttpStatusCode.Conflict,
                $"Selected rearrangement slot '{cmd.NewSlotId}' does not belong to this booking.",
                Errors.SlotNoLongerAvailable);
        }

        return Result<SelectedRearrangementOption>.Ok(new SelectedRearrangementOption(selectedSlot, optionTransaction));
    }

    private async Task<Result<ConfirmedBookingContext>> CreateAndConfirmNewBookingAsync(
        RearrangeBookingCommand cmd,
        string transactionRef,
        CancellationToken ct)
    {
        var holdResult = await _create.HandleAsync(new CreateHoldCommand
        {
            SlotId = cmd.NewSlotId.Trim(),
            TransactionRef = transactionRef,
            ActorContext = cmd.ActorContext
        }, ct);

        if (!holdResult.IsSuccess || holdResult.Value is null)
            return SlotUnavailableLike<ConfirmedBookingContext>(
                holdResult.StatusCode,
                holdResult.ErrorMessage ?? "Unable to create hold for new slot.",
                holdResult.ErrorCode);

        var confirmResult = await _confirm.HandleAsync(new ConfirmBookingCommand
        {
            HoldId = holdResult.Value.BookingId,
            Notes = "Rearranged",
            ActorContext = cmd.ActorContext
        }, ct);

        if (!confirmResult.IsSuccess || confirmResult.Value is null)
            return SlotUnavailableLike<ConfirmedBookingContext>(
                confirmResult.StatusCode,
                confirmResult.ErrorMessage ?? "Unable to confirm new booking.",
                confirmResult.ErrorCode);

        var newHold = await _holds.GetAsync(holdResult.Value.BookingId, ct);
        if (newHold is null)
            return Result<ConfirmedBookingContext>.Fail(HttpStatusCode.Conflict, "New booking hold was not found after confirmation.", Errors.Conflict);

        var newSlot = await _slots.GetAsync(newHold.SlotId, ct);
        if (newSlot is null)
            return Result<ConfirmedBookingContext>.Fail(HttpStatusCode.Conflict, "New slot was not found after confirmation.", Errors.Conflict);

        return Result<ConfirmedBookingContext>.Ok(new ConfirmedBookingContext(
            newHold,
            newSlot,
            confirmResult.Value.BookingReference ?? holdResult.Value.BookingReference));
    }

    private async Task<Result> CancelPreviousBookingAsync(
        RearrangeBookingCommand cmd,
        string previousBookingId,
        CancellationToken ct)
    {
        var cancelResult = await _cancel.CancelAsync(new CancelBookingCommand
        {
            BookingId = previousBookingId,
            ActorContext = cmd.ActorContext,
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
        string workflowKey,
        string notificationSummary,
        CancellationToken ct)
    {
        var eventId = await _lifecycle.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: newBooking.Hold.Id,
            TransactionId: existingBooking.Transaction.Id,
            EventType: LifecycleEventTypes.Rearranged,
            ActorContext: cmd.ActorContext,
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
                previousAdviserId = existingBooking.Slot.AdviserId,
                previousAdviserName = existingBooking.Slot.AdviserName,
                previousStartUtc = existingBooking.Slot.StartUtc,
                previousEndUtc = existingBooking.Slot.EndUtc,
                newAdviserId = newBooking.Slot.AdviserId,
                newAdviserName = newBooking.Slot.AdviserName,
                newStartUtc = newBooking.Slot.StartUtc,
                newEndUtc = newBooking.Slot.EndUtc,
                notificationSummary
            },
            OccurredUtc: _clock.UtcNow,
            CorrelationId: cmd.CorrelationId,
            SourceSystem: cmd.ActorContext?.SourceApplication ?? "BookingService",
            RelatedBookingId: existingBooking.Hold.Id,
            PreviousState: LifecycleStates.Booked,
            NewState: LifecycleStates.Rearranged,
            TriggerReason: workflowKey,
            PartnerName: cmd.ActorContext?.PartnerName), ct);

        var now = _clock.UtcNow;
        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Outlook,
            1,
            LifecycleStepStatuses.Succeeded,
            now,
            now,
            CorrelationId: cmd.CorrelationId,
            ActorContext: cmd.ActorContext), ct);
        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.SqlAudit,
            2,
            LifecycleStepStatuses.Succeeded,
            now,
            now,
            CorrelationId: cmd.CorrelationId,
            ActorContext: cmd.ActorContext), ct);

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
        BookingWorkflowNotificationOutcome? outcome = null;

        try
        {
            var client = _clients is null
                ? null
                : await _clients.GetAsync(existingBooking.Transaction.TransactionRef, ct);

            outcome = await _notifications.RequestAsync(
                new BookingWorkflowNotificationRequest(
                    LifecycleEventTypes.Rearranged,
                    newBookingId,
                    ResolveNotificationActorType(cmd),
                    BuildBookingRescheduledRecipients(client),
                    BuildBookingRescheduledNotificationData(cmd, existingBooking, newBooking, eventId, notificationSummary, client)),
                ct);
        }
        catch (Exception)
        {
            outcome = BookingWorkflowNotificationOutcome.Failed(
                "BookingRescheduled",
                0,
                failureCode: LifecycleErrorCodes.NotificationFailed);
        }

        if (outcome is not null)
        {
            notificationStatus = outcome.ToLifecycleStepStatus();
            notificationErrorCode = outcome.ToLifecycleStepErrorCode();
            notificationErrorDetails = outcome.ToLifecycleStepDetails();
        }

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Notifications,
            3,
            notificationStatus,
            notificationStartedUtc,
            _clock.UtcNow,
            notificationErrorCode,
            notificationErrorDetails,
            cmd.CorrelationId,
            cmd.ActorContext), ct);
    }

    private static IReadOnlyList<BookingNotificationRecipient> BuildBookingRescheduledRecipients(
        Domain.Client.ClientDirectoryItem? client)
    {
        if (client is null)
            return Array.Empty<BookingNotificationRecipient>();

        var displayName = $"{client.FirstName} {client.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = null;

        return
        [
            new BookingNotificationRecipient(
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
        string notificationSummary,
        Domain.Client.ClientDirectoryItem? client)
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

        var data = new Dictionary<string, string>
        {
            ["eventId"] = eventId,
            ["bookingId"] = newBooking.Hold.Id,
            ["previousBookingId"] = existingBooking.Hold.Id,
            ["newBookingId"] = newBooking.Hold.Id,
            ["previousSlotId"] = existingBooking.Slot.Id,
            ["newSlotId"] = newBooking.Slot.Id,
            ["IdempotencyKey"] = BookingWorkflowIdempotencyKeys.Notification("booking-rescheduled", newBooking.Hold.Id),
            ["adviserName"] = newBooking.Slot.AdviserName,
            ["startUtc"] = newBooking.Slot.StartUtc.ToString("O"),
            ["endUtc"] = newBooking.Slot.EndUtc.ToString("O"),
            ["greetingName"] = "there",
            ["whenLine"] = dataByPrefix.GetValueOrDefault("When", string.Empty),
            ["locationLine"] = dataByPrefix.GetValueOrDefault("Meeting type", string.Empty),
            ["note"] = note,
            ["manageBookingLinks"] = string.Empty
        };

        BookingNotificationPayloadFields.AddStandardBookingFields(
            data,
            existingBooking.Transaction,
            newBooking.Slot,
            "Rescheduled");

        AddClientAndMeetingLocation(data, existingBooking.Transaction, client);
        return data;
    }

    private static void AddClientAndMeetingLocation(
        Dictionary<string, string> data,
        BookingTransaction transaction,
        Domain.Client.ClientDirectoryItem? client)
    {
        data["clientName"] = FirstNonEmpty(
            transaction.ClientName,
            BuildClientDisplayName(client));
        data["clientEmail"] = FirstNonEmpty(transaction.ClientEmail, client?.Email);
        data["clientPhone"] = client?.Phone?.Trim() ?? string.Empty;
        data["meetingAddressLine1"] = FirstNonEmpty(transaction.ClientAddressLine1, client?.StreetName1);
        data["meetingAddressLine2"] = FirstNonEmpty(transaction.ClientAddressLine2, client?.StreetName2);
        data["meetingTown"] = FirstNonEmpty(transaction.ClientTown, client?.Town);
        data["meetingCounty"] = FirstNonEmpty(transaction.ClientCounty, client?.County);
        data["meetingPostcode"] = FirstNonEmpty(transaction.ClientPostcode, client?.PostalCode);
        data["meetingAddress"] = BuildMeetingAddress(data);
    }

    private static string BuildClientDisplayName(Domain.Client.ClientDirectoryItem? client)
        => client is null
            ? string.Empty
            : FirstNonEmpty($"{client.FirstName} {client.LastName}".Trim(), client.Email);

    private static string BuildMeetingAddress(Dictionary<string, string> data)
    {
        string Get(string key) => data.TryGetValue(key, out var value) ? value : string.Empty;

        return string.Join(", ", new[]
        {
            Get("meetingAddressLine1"),
            Get("meetingAddressLine2"),
            Get("meetingTown"),
            Get("meetingCounty"),
            Get("meetingPostcode")
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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
                transactionRef = existingBooking.Transaction.TransactionRef,
                performedBy = DownstreamPerformedByResolver.Resolve(cmd.ActorContext, cmd.RequestedBy),
                bookingReference = newBooking.BookingReference ?? newBooking.Hold.Reference ?? existingBooking.Transaction.TransactionRef,
                previousSlotId = existingBooking.Slot.Id,
                newSlotId = newBooking.Slot.Id,
                previousStartUtc = existingBooking.Slot.StartUtc,
                previousEndUtc = existingBooking.Slot.EndUtc,
                newStartUtc = newBooking.Slot.StartUtc,
                newEndUtc = newBooking.Slot.EndUtc,
                meetingType = existingBooking.Transaction.MeetingType,
                meetingMode = existingBooking.Transaction.IsRemote ? "online" : "face-to-face",
                previousAdviserId = existingBooking.Slot.AdviserId,
                newAdviserId = newBooking.Slot.AdviserId,
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
            PreviousBookingReference = existingBooking.Transaction.BookingReference ?? existingBooking.Hold.Reference,
            NewBookingId = newBooking.Hold.Id,
            NewBookingReference = newBooking.BookingReference ?? newBooking.Hold.Reference,
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

    private static Result<RearrangeBookingResponse> TryBuildIdempotentRearrangeResponse(LifecycleEventRecord existing)
    {
        if (string.IsNullOrWhiteSpace(existing.AfterJson))
        {
            return Result<RearrangeBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "A previous rearrangement was found, but its result could not be reconstructed.",
                Errors.Conflict);
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<RearrangedSnapshot>(existing.AfterJson);
            if (snapshot is null ||
                string.IsNullOrWhiteSpace(snapshot.previousBookingId) ||
                string.IsNullOrWhiteSpace(snapshot.newBookingId) ||
                string.IsNullOrWhiteSpace(snapshot.newSlotId))
            {
                return Result<RearrangeBookingResponse>.Fail(
                    HttpStatusCode.Conflict,
                    "A previous rearrangement was found, but its result could not be reconstructed.",
                    Errors.Conflict);
            }

            return Result<RearrangeBookingResponse>.Ok(new RearrangeBookingResponse
            {
                PreviousBookingId = snapshot.previousBookingId,
                NewBookingId = snapshot.newBookingId,
                NewSlotId = snapshot.newSlotId,
                PreviousAdviserId = snapshot.previousAdviserId ?? string.Empty,
                PreviousAdviserName = snapshot.previousAdviserName ?? string.Empty,
                PreviousStartUtc = snapshot.previousStartUtc,
                PreviousEndUtc = snapshot.previousEndUtc,
                NewAdviserId = snapshot.newAdviserId ?? string.Empty,
                NewAdviserName = snapshot.newAdviserName ?? string.Empty,
                NewStartUtc = snapshot.newStartUtc,
                NewEndUtc = snapshot.newEndUtc,
                NotificationSummary = snapshot.notificationSummary ?? string.Empty
            });
        }
        catch (JsonException)
        {
            return Result<RearrangeBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "A previous rearrangement was found, but its result could not be reconstructed.",
                Errors.Conflict);
        }
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

    private static Result<T> SlotUnavailableLike<T>(
        HttpStatusCode statusCode,
        string message,
        string? errorCode)
    {
        if (statusCode != HttpStatusCode.Conflict)
            return Result<T>.Fail(statusCode, message, errorCode);

        return Result<T>.Fail(statusCode, message, Errors.SlotNoLongerAvailable);
    }

    private sealed record ExistingBookingContext(
        BookingHold Hold,
        BookingSlot Slot,
        BookingTransaction Transaction);

    private sealed record SelectedRearrangementOption(
        BookingSlot Slot,
        BookingTransaction Transaction);

    private sealed record ConfirmedBookingContext(
        BookingHold Hold,
        BookingSlot Slot,
        string? BookingReference);

    private sealed record RearrangedSnapshot(
        string? previousBookingId,
        string? newBookingId,
        string? newSlotId,
        string? previousAdviserId,
        string? previousAdviserName,
        DateTime previousStartUtc,
        DateTime previousEndUtc,
        string? newAdviserId,
        string? newAdviserName,
        DateTime newStartUtc,
        DateTime newEndUtc,
        string? notificationSummary);
}
