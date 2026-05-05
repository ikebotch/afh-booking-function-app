using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

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
    private readonly INotificationService _notifications;
    private readonly IDownstreamUpdateService _downstreamUpdates;
    private readonly ILifecycleAuditService _audit;
    private readonly ILogger<CancellationOrchestrator> _logger;

    public CancellationOrchestrator(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IUnitOfWork uow,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IClock clock,
        INotificationService notifications,
        IDownstreamUpdateService downstreamUpdates,
        ILifecycleAuditService audit,
        ILogger<CancellationOrchestrator> logger)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _uow = uow;
        _calendar = calendar;
        _profiles = profiles;
        _clock = clock;
        _notifications = notifications;
        _downstreamUpdates = downstreamUpdates;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result<CancelBookingResponse>> CancelAsync(
        CancelBookingCommand cmd,
        bool sendClientNotification,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);

        var validation = BookingChangeValidation.Validate(cmd);
        if (!validation.IsSuccess)
            return Result<CancelBookingResponse>.Fail(validation.StatusCode, validation.ErrorMessage!, validation.ErrorCode);

        var utcNow = _clock.UtcNow;
        var hold = await _holds.GetAsync(cmd.BookingId, ct);
        if (hold is null)
            return Result<CancelBookingResponse>.NotFound($"Hold '{cmd.BookingId}' was not found.");

        if (hold.Status == BookingHoldStatus.Cancelled)
        {
            return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
            {
                BookingId = hold.Id,
                Status = hold.Status.ToString(),
                CancelledUtc = hold.CancelledUtc ?? utcNow
            });
        }

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result<CancelBookingResponse>.Fail(HttpStatusCode.Conflict, "Hold has no slotId linked.", Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<CancelBookingResponse>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' linked to hold was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<CancelBookingResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' linked to slot was not found.", Errors.Conflict);

        var before = CreateSnapshot(hold, slot, tx);
        var outlookStartedUtc = _clock.UtcNow;
        var outlookStatus = LifecycleStepStatuses.Skipped;
        string? outlookErrorCode = null;
        string? outlookErrorDetails = null;

        hold.Cancel(cmd.Reason ?? cmd.ReasonCode ?? "Cancelled", utcNow);

        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            try
            {
                var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(slot.AdviserId, ct);
                await _calendar.CancelBookingEventAsync(calendarUserId, hold.CalendarProviderEventId!, ct);
                outlookStatus = LifecycleStepStatuses.Succeeded;
            }
            catch (Exception ex)
            {
                outlookStatus = LifecycleStepStatuses.Failed;
                outlookErrorCode = LifecycleErrorCodes.CalendarCancelFailed;
                outlookErrorDetails = ex.Message;
                _logger.LogWarning(ex, "Failed to cancel calendar event for HoldId={HoldId}. Continuing with lifecycle persistence.", hold.Id);
            }
        }

        await _holds.UpdateAsync(hold, ct);

        var eventId = await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: hold.Id,
            TransactionId: tx.Id,
            EventType: LifecycleEventTypes.Cancelled,
            ActorType: string.IsNullOrWhiteSpace(cmd.RequestedBy) ? LifecycleActors.Unknown : cmd.RequestedBy,
            ActorId: cmd.ActorId,
            ReasonCode: cmd.ReasonCode,
            ReasonNotes: cmd.ReasonDetail ?? cmd.Reason,
            Before: before,
            After: CreateSnapshot(hold, slot, tx),
            OccurredUtc: utcNow,
            CorrelationId: cmd.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: null,
            PreviousState: ResolveLifecycleStateBeforeCancellation(before),
            NewState: LifecycleStates.Cancelled), ct);

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.Outlook,
            1,
            outlookStatus,
            outlookStartedUtc,
            _clock.UtcNow,
            outlookErrorCode,
            outlookErrorDetails,
            cmd.CorrelationId), ct);

        var sqlCompletedUtc = _clock.UtcNow;
        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.SqlAudit,
            2,
            LifecycleStepStatuses.Succeeded,
            utcNow,
            sqlCompletedUtc,
            null,
            null,
            cmd.CorrelationId), ct);

        await _uow.SaveChangesAsync(ct);

        var notificationStartedUtc = _clock.UtcNow;
        var notificationStepStatus = LifecycleStepStatuses.Skipped;
        string? notificationStepError = null;
        string? notificationStepDetails = null;

        if (sendClientNotification)
        {
            try
            {
                var notificationMessage = BuildCancellationNotification(slot, cmd);
                var dispatch = await _notifications.SendBookingNotificationAsync(
                    new NotificationDispatchRequest(
                        hold.Id,
                        LifecycleEventTypes.Cancelled,
                        notificationMessage,
                        true,
                        true,
                        eventId,
                        cmd.CorrelationId),
                    ct);

                notificationStepStatus = dispatch.SmsStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
                    dispatch.EmailStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)
                    ? LifecycleStepStatuses.Failed
                    : LifecycleStepStatuses.Succeeded;
            }
            catch (Exception ex)
            {
                notificationStepStatus = LifecycleStepStatuses.Failed;
                notificationStepError = LifecycleErrorCodes.NotificationFailed;
                notificationStepDetails = ex.Message;
                _logger.LogWarning(ex, "Notification dispatch failed for HoldId={HoldId}", hold.Id);
            }
        }

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.Notifications,
            3,
            notificationStepStatus,
            notificationStartedUtc,
            _clock.UtcNow,
            notificationStepError,
            notificationStepDetails,
            cmd.CorrelationId), ct);

        await _uow.SaveChangesAsync(ct);

        await _downstreamUpdates.PublishBookingChangeAsync(
            bookingId: hold.Id,
            changeType: "Cancel",
            transactionRef: tx.TransactionRef,
            payloadJson: JsonSerializer.Serialize(new
            {
                bookingId = hold.Id,
                slotId = slot.Id,
                adviserId = slot.AdviserId,
                cancelledUtc = hold.CancelledUtc,
                reasonCode = cmd.ReasonCode,
                reasonNotes = cmd.ReasonDetail ?? cmd.Reason,
                lifecycleEventId = eventId
            }),
            ct: ct);

        return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
        {
            BookingId = hold.Id,
            Status = hold.Status.ToString(),
            CancelledUtc = hold.CancelledUtc ?? utcNow
        });
    }

    private static object CreateSnapshot(Domain.Bookings.BookingHold hold, Domain.Transactions.BookingSlot slot, Domain.Transactions.BookingTransaction tx)
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

    private static string BuildCancellationNotification(Domain.Transactions.BookingSlot slot, CancelBookingCommand cmd)
    {
        var reason = string.IsNullOrWhiteSpace(cmd.ReasonCode)
            ? string.Empty
            : $" Reason: {cmd.ReasonCode}{(string.IsNullOrWhiteSpace(cmd.ReasonDetail) ? string.Empty : $" - {cmd.ReasonDetail!.Trim()}")}.";

        return $"Your meeting with {slot.AdviserName} on {slot.StartUtc:yyyy-MM-dd HH:mm} has been cancelled.{reason}";
    }
}
