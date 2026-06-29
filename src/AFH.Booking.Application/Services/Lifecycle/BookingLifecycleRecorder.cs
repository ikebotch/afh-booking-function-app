using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle;

namespace AFH.Booking.Application.Lifecycle;

public sealed class BookingLifecycleRecorder : IBookingLifecycleRecorder
{
    private readonly ILifecycleAuditService _audit;

    public BookingLifecycleRecorder(ILifecycleAuditService audit)
    {
        _audit = audit;
    }

    public Task<string> RecordEventAsync(BookingLifecycleEventRecord entry, CancellationToken ct)
    {
        var actor = entry.ActorContext;
        var newState = string.IsNullOrWhiteSpace(entry.NewState) &&
                       BookingLifecycleStateMachine.TryResolveStateForEventType(entry.EventType, out var resolvedState)
            ? resolvedState
            : entry.NewState;

        return _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: entry.BookingId,
            TransactionId: entry.TransactionId,
            EventType: entry.EventType,
            ActorType: actor?.ActorType ?? entry.ActorType,
            ActorId: actor?.ActorId ?? entry.ActorId,
            ReasonCode: entry.ReasonCode,
            ReasonNotes: entry.ReasonNotes,
            Before: entry.Before,
            After: entry.After,
            OccurredUtc: entry.OccurredUtc,
            CorrelationId: actor?.CorrelationId ?? entry.CorrelationId,
            SourceSystem: actor?.SourceApplication ?? entry.SourceSystem,
            RelatedBookingId: entry.RelatedBookingId,
            PreviousState: entry.PreviousState,
            NewState: newState,
            TriggerReason: entry.TriggerReason,
            PartnerName: actor?.PartnerName ?? entry.PartnerName), ct);
    }

    public Task RecordStepAsync(string lifecycleEventId, BookingLifecycleStepRecord step, CancellationToken ct)
    {
        return _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            LifecycleEventId: lifecycleEventId,
            StepName: step.StepName,
            Sequence: step.Sequence,
            Status: step.Status,
            StartedUtc: step.StartedUtc,
            CompletedUtc: step.CompletedUtc,
            ErrorCode: step.ErrorCode,
            ErrorDetails: step.ErrorDetails,
            CorrelationId: step.ActorContext?.CorrelationId ?? step.CorrelationId), ct);
    }
}
