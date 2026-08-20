using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Services.AdviserProjection;
using AFH.Booking.Application.Services.Notifications;
using AFH.Booking.Domain.Bookings.Commands;
using System.Text.Json;

namespace AFH.Booking.Application.Bookings;

public sealed class CancellationOrchestrator : ICancellationOrchestrator
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IUnitOfWork _uow;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IClock _clock;
    private readonly IBookingWorkflowNotificationAdapter _notifications;
    private readonly IClientDirectory? _clients;
    private readonly IDownstreamUpdateService _downstreamUpdates;
    private readonly IBookingLifecycleRecorder _lifecycle;
    private readonly ILogger<CancellationOrchestrator> _logger;
    private readonly IBookingWorkflowIdempotencyGuard? _idempotency;

    public CancellationOrchestrator(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IUnitOfWork uow,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IClock clock,
        IBookingWorkflowNotificationAdapter notifications,
        IDownstreamUpdateService downstreamUpdates,
        IBookingLifecycleRecorder lifecycle,
        ILogger<CancellationOrchestrator> logger,
        IClientDirectory? clients = null,
        IBookingWorkflowIdempotencyGuard? idempotency = null)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _uow = uow;
        _calendar = calendar;
        _profiles = profiles;
        _clock = clock;
        _notifications = notifications;
        _clients = clients;
        _downstreamUpdates = downstreamUpdates;
        _lifecycle = lifecycle;
        _logger = logger;
        _idempotency = idempotency;
    }

    public async Task<Result<CancelBookingResponse>> CancelAsync(
        CancelBookingCommand cmd,
        bool sendClientNotification,
        CancellationToken ct)
    {
        var utcNow = _clock.UtcNow;
        var workflowKey = BookingWorkflowIdempotencyKeys.Cancellation(cmd.BookingId, cmd.RequestedBy, cmd.ReasonCode);
        var existing = await FindCompletedCancellationAsync(workflowKey, cmd, ct);
        if (existing is not null)
            return OkResponse(existing, utcNow);

        var holdResult = await LoadCancellationHoldAsync(cmd, ct);
        if (!holdResult.IsSuccess || holdResult.Value is null)
            return FailLike<BookingHold, CancelBookingResponse>(holdResult);

        var hold = holdResult.Value;
        if (hold.Status == BookingHoldStatus.Cancelled)
        {
            if (string.Equals(cmd.RequestedBy, LifecycleActors.Client, StringComparison.OrdinalIgnoreCase))
            {
                return Result<CancelBookingResponse>.Fail(
                    HttpStatusCode.Conflict,
                    $"Booking '{hold.Id}' is already cancelled.",
                    Errors.Conflict);
            }

            return OkResponse(hold, utcNow);
        }

        var actionable = BookingSelfServiceStatusRules.EnsureActionable(hold, "cancelled");
        if (!actionable.IsSuccess)
            return FailLike<CancelBookingResponse>(actionable);

        var contextResult = await LoadCancellationContextAsync(hold, ct);
        if (!contextResult.IsSuccess || contextResult.Value is null)
            return FailLike<CancellationContext, CancelBookingResponse>(contextResult);

        var context = contextResult.Value;

        var before = CreateSnapshot(context.Hold, context.Slot, context.Transaction);
        context.Hold.Cancel(cmd.Reason ?? cmd.ReasonCode ?? "Cancelled", utcNow);
        var outlookStep = await CancelCalendarEventIfPresentAsync(context, ct);

        await _holds.UpdateAsync(context.Hold, ct);

        var eventId = await RecordCancellationLifecycleAsync(
            cmd,
            context,
            before,
            outlookStep,
            workflowKey,
            utcNow,
            ct);

        await _uow.SaveChangesAsync(ct);

        await RecordCancellationNotificationStepAsync(
            cmd,
            context,
            eventId,
            sendClientNotification,
            ct);

        await _uow.SaveChangesAsync(ct);

        await PublishCancellationUpdateAsync(cmd, context, eventId, ct);

        return OkResponse(context.Hold, context.Transaction, utcNow);
    }

    private async Task<Result<BookingHold>> LoadCancellationHoldAsync(
        CancelBookingCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
        {
            return Result<BookingHold>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);
        }

        var validation = BookingChangeValidation.Validate(cmd);
        if (!validation.IsSuccess)
            return FailLike<BookingHold>(validation);

        var hold = await _holds.GetAsync(cmd.BookingId, ct);
        if (hold is null)
            return Result<BookingHold>.NotFound($"Hold '{cmd.BookingId}' was not found.");

        return Result<BookingHold>.Ok(hold);
    }

    private async Task<Result<CancellationContext>> LoadCancellationContextAsync(
        BookingHold hold,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result<CancellationContext>.Fail(HttpStatusCode.Conflict, "Hold has no slotId linked.", Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<CancellationContext>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' linked to hold was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<CancellationContext>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' linked to slot was not found.", Errors.Conflict);

        return Result<CancellationContext>.Ok(new CancellationContext(hold, slot, tx));
    }

    private async Task<LifecycleStepOutcome> CancelCalendarEventIfPresentAsync(
        CancellationContext context,
        CancellationToken ct)
    {
        var startedUtc = _clock.UtcNow;
        var status = LifecycleStepStatuses.Skipped;
        string? errorCode = null;
        string? errorDetails = null;

        if (string.IsNullOrWhiteSpace(context.Hold.CalendarProviderEventId))
            return new LifecycleStepOutcome(startedUtc, status, errorCode, errorDetails);

        try
        {
            var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(context.Slot.AdviserId, ct);
            await _calendar.CancelBookingEventAsync(calendarUserId, context.Hold.CalendarProviderEventId!, ct);
            status = LifecycleStepStatuses.Succeeded;
        }
        catch (Exception ex)
        {
            status = LifecycleStepStatuses.Failed;
            errorCode = LifecycleErrorCodes.CalendarCancelFailed;
            errorDetails = ex.Message;
            _logger.LogWarning(ex, "Failed to cancel calendar event for HoldId={HoldId}. Continuing with lifecycle persistence.", context.Hold.Id);
        }

        return new LifecycleStepOutcome(startedUtc, status, errorCode, errorDetails);
    }

    private async Task<string> RecordCancellationLifecycleAsync(
        CancelBookingCommand cmd,
        CancellationContext context,
        object before,
        LifecycleStepOutcome outlookStep,
        string workflowKey,
        DateTime utcNow,
        CancellationToken ct)
    {
        var eventId = await _lifecycle.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: context.Hold.Id,
            TransactionId: context.Transaction.Id,
            EventType: LifecycleEventTypes.Cancelled,
            ActorContext: cmd.ActorContext,
            ActorType: string.IsNullOrWhiteSpace(cmd.RequestedBy) ? LifecycleActors.Unknown : cmd.RequestedBy,
            ActorId: cmd.ActorId,
            ReasonCode: cmd.ReasonCode,
            ReasonNotes: cmd.ReasonDetail ?? cmd.Reason,
            Before: before,
            After: CreateSnapshot(context.Hold, context.Slot, context.Transaction),
            OccurredUtc: utcNow,
            CorrelationId: cmd.CorrelationId,
            SourceSystem: cmd.ActorContext?.SourceApplication ?? "BookingService",
            RelatedBookingId: null,
            PreviousState: ResolveLifecycleStateBeforeCancellation(before),
            NewState: LifecycleStates.Cancelled,
            TriggerReason: workflowKey,
            PartnerName: cmd.ActorContext?.PartnerName), ct);

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Outlook,
            1,
            outlookStep.Status,
            outlookStep.StartedUtc,
            _clock.UtcNow,
            outlookStep.ErrorCode,
            outlookStep.ErrorDetails,
            cmd.CorrelationId,
            cmd.ActorContext), ct);

        var sqlCompletedUtc = _clock.UtcNow;
        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.SqlAudit,
            2,
            LifecycleStepStatuses.Succeeded,
            utcNow,
            sqlCompletedUtc,
            CorrelationId: cmd.CorrelationId,
            ActorContext: cmd.ActorContext), ct);

        return eventId;
    }

    private async Task RecordCancellationNotificationStepAsync(
        CancelBookingCommand cmd,
        CancellationContext context,
        string eventId,
        bool sendClientNotification,
        CancellationToken ct)
    {
        var notificationStartedUtc = _clock.UtcNow;
        var notificationStepStatus = LifecycleStepStatuses.Skipped;
        string? notificationStepError = null;
        string? notificationStepDetails = null;
        BookingWorkflowNotificationOutcome? outcome = sendClientNotification
            ? null
            : BookingWorkflowNotificationOutcome.NotRequested("BookingCancelled");

        if (sendClientNotification)
        {
            try
            {
                var client = _clients is null
                    ? null
                    : await _clients.GetAsync(context.Transaction.TransactionRef, ct);

                outcome = await _notifications.RequestAsync(
                    new BookingWorkflowNotificationRequest(
                        LifecycleEventTypes.Cancelled,
                        context.Hold.Id,
                        ResolveNotificationActorType(cmd),
                        BuildBookingCancelledRecipients(client),
                        BuildBookingCancelledNotificationData(cmd, context, eventId, client)),
                    ct);
            }
            catch (Exception ex)
            {
                outcome = BookingWorkflowNotificationOutcome.Failed(
                    "BookingCancelled",
                    0,
                    failureCode: LifecycleErrorCodes.NotificationFailed);
                _logger.LogWarning(ex, "Notification dispatch failed for HoldId={HoldId}", context.Hold.Id);
            }
        }

        if (outcome is not null)
        {
            notificationStepStatus = outcome.ToLifecycleStepStatus();
            notificationStepError = outcome.ToLifecycleStepErrorCode();
            notificationStepDetails = outcome.ToLifecycleStepDetails();
        }

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Notifications,
            3,
            notificationStepStatus,
            notificationStartedUtc,
            _clock.UtcNow,
            notificationStepError,
            notificationStepDetails,
            cmd.CorrelationId,
            cmd.ActorContext), ct);
    }

    private async Task PublishCancellationUpdateAsync(
        CancelBookingCommand cmd,
        CancellationContext context,
        string eventId,
        CancellationToken ct)
    {
        await _downstreamUpdates.PublishBookingChangeAsync(
            bookingId: context.Hold.Id,
            changeType: "Cancel",
            transactionRef: context.Transaction.TransactionRef,
            payloadJson: JsonSerializer.Serialize(new
            {
                bookingId = context.Hold.Id,
                bookingReference = context.Transaction.BookingReference ?? context.Hold.Reference ?? context.Transaction.TransactionRef,
                slotId = context.Slot.Id,
                adviserId = context.Slot.AdviserId,
                startUtc = context.Slot.StartUtc,
                endUtc = context.Slot.EndUtc,
                meetingType = context.Transaction.MeetingType,
                cancelledUtc = context.Hold.CancelledUtc,
                reasonCode = cmd.ReasonCode,
                reasonNotes = cmd.ReasonDetail ?? cmd.Reason,
                lifecycleEventId = eventId
            }),
            ct: ct);
    }

    private static Result<CancelBookingResponse> OkResponse(BookingHold hold, BookingTransaction tx, DateTime utcNow)
    {
        return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
        {
            BookingId = hold.Id,
            BookingReference = tx.BookingReference ?? hold.Reference,
            Status = hold.Status.ToString(),
            CancelledUtc = hold.CancelledUtc ?? utcNow
        });
    }

    private static Result<CancelBookingResponse> OkResponse(BookingHold hold, DateTime utcNow)
    {
        return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
        {
            BookingId = hold.Id,
            BookingReference = hold.Reference,
            Status = hold.Status.ToString(),
            CancelledUtc = hold.CancelledUtc ?? utcNow
        });
    }

    private async Task<LifecycleEventRecord?> FindCompletedCancellationAsync(
        string workflowKey,
        CancelBookingCommand cmd,
        CancellationToken ct)
    {
        if (_idempotency is null ||
            string.Equals(cmd.RequestedBy, LifecycleActors.Client, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await _idempotency.FindCompletedAsync(workflowKey, ct);
    }

    private static Result<CancelBookingResponse> OkResponse(LifecycleEventRecord existing, DateTime utcNow)
    {
        return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
        {
            BookingId = existing.BookingId,
            BookingReference = null,
            Status = BookingHoldStatus.Cancelled.ToString(),
            CancelledUtc = existing.OccurredUtc == default ? utcNow : existing.OccurredUtc
        });
    }

    private static object CreateSnapshot(Domain.Bookings.BookingHold hold, BookingSlot slot, BookingTransaction tx)
    {
        return new
        {
            bookingId = hold.Id,
            holdStatus = hold.Status.ToString(),
            holdCancelledUtc = hold.CancelledUtc,
            slotId = slot.Id,
            slotStartUtc = slot.StartUtc,
            slotEndUtc = slot.EndUtc,
            adviserId = slot.AdviserId,
            transactionId = tx.Id,
            transactionRef = tx.TransactionRef,
            transactionStatus = tx.Status.ToString()
        };
    }

    private static string? ResolveLifecycleStateBeforeCancellation(object before)
    {
        var status = before.GetType().GetProperty("holdStatus")?.GetValue(before)?.ToString();
        return status switch
        {
            nameof(BookingHoldStatus.Confirmed) => LifecycleStates.Booked,
            nameof(BookingHoldStatus.Cancelled) => LifecycleStates.Cancelled,
            _ => null
        };
    }

    private static string BuildCancellationNotification(BookingSlot slot, CancelBookingCommand cmd)
    {
        var reason = string.IsNullOrWhiteSpace(cmd.ReasonCode)
            ? string.Empty
            : $" Reason: {cmd.ReasonCode}{(string.IsNullOrWhiteSpace(cmd.ReasonDetail) ? string.Empty : $" - {cmd.ReasonDetail!.Trim()}")}.";

        return $"Your meeting with {slot.AdviserName} on {slot.StartUtc:yyyy-MM-dd HH:mm} has been cancelled.{reason}";
    }

    private static string ResolveNotificationActorType(CancelBookingCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.RequestedBy))
            return LifecycleActors.System;

        return cmd.RequestedBy.Trim();
    }

    private static IReadOnlyList<BookingNotificationRecipient> BuildBookingCancelledRecipients(
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

    private static IReadOnlyDictionary<string, string> BuildBookingCancelledNotificationData(
        CancelBookingCommand cmd,
        CancellationContext context,
        string eventId,
        Domain.Client.ClientDirectoryItem? client)
    {
        var notificationMessage = BuildCancellationNotification(context.Slot, cmd);
        var template = BookingNotificationEmailTemplate.Build(
            eventType: "Cancelled",
            clientDisplayName: null,
            adviserName: context.Slot.AdviserName,
            startUtc: context.Slot.StartUtc,
            endUtc: context.Slot.EndUtc,
            timezoneId: context.Transaction.Timezone,
            isRemote: context.Transaction.IsRemote,
            customMessage: notificationMessage);
        var lines = template.TextBody.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var dataByPrefix = lines
            .Select(line => line.Split(": ", 2, StringSplitOptions.None))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var data = new Dictionary<string, string>
        {
            ["eventId"] = eventId,
            ["bookingId"] = context.Hold.Id,
            ["slotId"] = context.Slot.Id,
            ["adviserId"] = context.Slot.AdviserId,
            ["adviserName"] = context.Slot.AdviserName,
            ["startUtc"] = context.Slot.StartUtc.ToString("O"),
            ["endUtc"] = context.Slot.EndUtc.ToString("O"),
            ["transactionRef"] = context.Transaction.TransactionRef,
            ["IdempotencyKey"] = BookingWorkflowIdempotencyKeys.Notification("booking-cancelled", context.Hold.Id),
            ["greetingName"] = "there",
            ["whenLine"] = dataByPrefix.GetValueOrDefault("When", string.Empty),
            ["locationLine"] = dataByPrefix.GetValueOrDefault("Meeting type", string.Empty),
            ["note"] = notificationMessage,
            ["manageBookingLinks"] = string.Empty,
            ["reasonCode"] = cmd.ReasonCode ?? string.Empty,
            ["reasonDetail"] = cmd.ReasonDetail ?? cmd.Reason ?? string.Empty
        };

        BookingNotificationPayloadFields.AddStandardBookingFields(
            data,
            context.Transaction,
            context.Slot,
            "Cancelled");

        AddClientAndMeetingLocation(data, context.Transaction, client);
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

    private sealed record CancellationContext(
        BookingHold Hold,
        BookingSlot Slot,
        BookingTransaction Transaction);

    private sealed record LifecycleStepOutcome(
        DateTime StartedUtc,
        string Status,
        string? ErrorCode,
        string? ErrorDetails);
}
