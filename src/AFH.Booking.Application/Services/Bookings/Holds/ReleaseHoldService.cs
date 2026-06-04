using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Services.AdviserProjection;
using AFH.Booking.Domain.Bookings.Commands;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Holds;

public sealed class ReleaseHoldService : IReleaseHoldService
{
    private readonly IBookingHoldRepository _holds;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IBookingLifecycleRecorder _lifecycle;
    private readonly ILogger<ReleaseHoldService> _logger;

    public ReleaseHoldService(
        IBookingHoldRepository holds,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IUnitOfWork uow,
        IClock clock,
        IBookingLifecycleRecorder lifecycle,
        ILogger<ReleaseHoldService> logger)
    {
        _holds = holds;
        _calendar = calendar;
        _profiles = profiles;
        _uow = uow;
        _clock = clock;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public Task<Result<ReleaseHoldResponse>> HandleAsync(string holdId, CancellationToken ct)
        => HandleAsync(new ReleaseHoldCommand
        {
            HoldId = holdId,
            ReasonCode = "ManualRelease",
            ReasonDetail = "Released by legacy hold release API.",
            ReleaseKind = ReleaseHoldKind.ManualRelease,
            ActorContext = BookingActorContext.InternalAdmin(
                actorId: "LegacyReleaseHoldApi")
        }, ct);

    public async Task<Result<ReleaseHoldResponse>> HandleAsync(ReleaseHoldCommand command, CancellationToken ct)
    {
        var holdId = command.HoldId;
        if (string.IsNullOrWhiteSpace(holdId))
            return Result<ReleaseHoldResponse>.Fail(
                HttpStatusCode.BadRequest,
                "holdId is required.",
                "validation_error");

        var hold = await _holds.GetForUpdateAsync(holdId, ct);

        if (hold is null)
            return Result<ReleaseHoldResponse>.NotFound($"Hold '{holdId}' not found.");

        // Idempotent behaviour
        if (hold.Status == BookingHoldStatus.Released)
        {
            return Result<ReleaseHoldResponse>.Ok(new ReleaseHoldResponse
            {
                BookingId = hold.Id
            });
        }

        if (hold.Status == BookingHoldStatus.Cancelled ||
            hold.Status == BookingHoldStatus.Expired)
        {
            return Result<ReleaseHoldResponse>.Ok(new ReleaseHoldResponse
            {
                BookingId = hold.Id
            });
        }

        if (hold.Status == BookingHoldStatus.Confirmed)
            return Result<ReleaseHoldResponse>.Fail(
                HttpStatusCode.Conflict,
                "Confirmed holds cannot be released.",
                "conflict");

        var utcNow = _clock.UtcNow;
        var workflowKey = BookingWorkflowIdempotencyKeys.HoldRelease(hold.Id, command.ReleaseKind.ToString());
        var before = BuildAuditSnapshot(hold);
        var outlookStep = await CancelCalendarEventIfPresentAsync(hold, ct);
        var reasonCode = ResolveReasonCode(command);
        var reasonDetail = ResolveReasonDetail(command);

        if (command.ReleaseKind == ReleaseHoldKind.Expiry)
        {
            hold.Expire(utcNow);
        }
        else
        {
            hold.Release(utcNow, reasonDetail);
        }

        var eventId = await _lifecycle.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: hold.Id,
            TransactionId: null,
            EventType: command.ReleaseKind == ReleaseHoldKind.Expiry
                ? LifecycleEventTypes.HoldExpired
                : LifecycleEventTypes.HoldReleased,
            ActorContext: command.ActorContext,
            ActorType: LifecycleActors.System,
            ActorId: null,
            ReasonCode: reasonCode,
            ReasonNotes: reasonDetail,
            Before: before,
            After: BuildAuditSnapshot(hold),
            OccurredUtc: utcNow,
            CorrelationId: null,
            SourceSystem: "BookingService",
            PreviousState: null,
            NewState: null,
            TriggerReason: command.ReleaseKind == ReleaseHoldKind.Expiry
                ? workflowKey
                : workflowKey), ct);

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Outlook,
            1,
            outlookStep.Status,
            outlookStep.StartedUtc,
            outlookStep.CompletedUtc,
            outlookStep.ErrorCode,
            outlookStep.ErrorDetails,
            ActorContext: command.ActorContext), ct);

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.SqlAudit,
            2,
            LifecycleStepStatuses.Succeeded,
            utcNow,
            _clock.UtcNow,
            ActorContext: command.ActorContext), ct);

        await _holds.UpdateAsync(hold, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ReleaseHoldResponse>.Ok(new ReleaseHoldResponse
        {
            BookingId = hold.Id
        });
    }

    private async Task<LifecycleStepOutcome> CancelCalendarEventIfPresentAsync(
        BookingHold hold,
        CancellationToken ct)
    {
        var startedUtc = _clock.UtcNow;
        var status = LifecycleStepStatuses.Skipped;
        string? errorCode = null;
        string? errorDetails = null;

        if (string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            return new LifecycleStepOutcome(
                status,
                startedUtc,
                _clock.UtcNow,
                errorCode,
                errorDetails);
        }

        try
        {
            var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(hold.UserId, ct);
            await _calendar.CancelBookingEventAsync(
                calendarUserId,
                hold.CalendarProviderEventId!,
                ct);
            status = LifecycleStepStatuses.Succeeded;
        }
        catch (Exception ex)
        {
            status = LifecycleStepStatuses.Failed;
            errorCode = LifecycleErrorCodes.CalendarCancelFailed;
            errorDetails = ex.Message;
            _logger.LogWarning(ex, "Failed to cancel calendar event for HoldId={HoldId}. Continuing with hold release.", hold.Id);
        }

        return new LifecycleStepOutcome(
            status,
            startedUtc,
            _clock.UtcNow,
            errorCode,
            errorDetails);
    }

    private static string ResolveReasonCode(ReleaseHoldCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ReasonCode))
            return command.ReasonCode.Trim();

        return command.ReleaseKind == ReleaseHoldKind.Expiry
            ? "HoldExpired"
            : "ManualRelease";
    }

    private static string ResolveReasonDetail(ReleaseHoldCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ReasonDetail))
            return command.ReasonDetail.Trim();

        return command.ReleaseKind == ReleaseHoldKind.Expiry
            ? "Expired by holds cleanup job."
            : "Released by user";
    }

    private static object BuildAuditSnapshot(BookingHold hold)
        => new
        {
            hold.Id,
            hold.SlotId,
            hold.UserId,
            Status = hold.Status.ToString(),
            hold.ExpiresUtc,
            hold.ReleasedUtc,
            hold.CancelledUtc,
            hold.CancelReason,
            hold.CalendarProviderEventId,
            hold.BookingId
        };

    private sealed record LifecycleStepOutcome(
        string Status,
        DateTime StartedUtc,
        DateTime CompletedUtc,
        string? ErrorCode,
        string? ErrorDetails);
}
