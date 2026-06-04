using System.Text.Json;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;

namespace AFH.Booking.Application.Lifecycle;

public sealed class LifecycleAuditService : ILifecycleAuditService
{
    private readonly ILifecycleEventRepository _events;
    private readonly ILifecycleStepRepository _steps;
    private readonly JsonSerializerOptions _jsonOptions;

    public LifecycleAuditService(
        ILifecycleEventRepository events,
        ILifecycleStepRepository steps,
        JsonSerializerOptions jsonOptions)
    {
        _events = events;
        _steps = steps;
        _jsonOptions = jsonOptions;
    }

    public async Task<string> RecordEventAsync(LifecycleAuditEntry entry, CancellationToken ct)
    {
        var newState = string.IsNullOrWhiteSpace(entry.NewState) &&
                       BookingLifecycleStateMachine.TryResolveStateForEventType(entry.EventType, out var resolvedState)
            ? resolvedState
            : entry.NewState;

        if (!string.IsNullOrWhiteSpace(newState))
            BookingLifecycleStateMachine.Validate(entry.PreviousState, newState);

        var id = Guid.NewGuid().ToString("N");
        var actorType = string.IsNullOrWhiteSpace(entry.ActorType)
            ? LifecycleActors.System
            : entry.ActorType.Trim();

        await _events.AddAsync(new LifecycleEventRecord
        {
            Id = id,
            BookingId = entry.BookingId,
            TransactionId = entry.TransactionId,
            EventType = entry.EventType,
            PreviousState = entry.PreviousState,
            NewState = newState,
            ActorType = actorType,
            ActorId = entry.ActorId,
            ReasonCode = entry.ReasonCode,
            ReasonNotes = entry.ReasonNotes,
            BeforeJson = Serialize(entry.Before),
            AfterJson = Serialize(entry.After),
            OccurredUtc = entry.OccurredUtc,
            CorrelationId = entry.CorrelationId,
            SourceSystem = entry.SourceSystem,
            RelatedBookingId = entry.RelatedBookingId,
            TriggerReason = entry.TriggerReason
        }, ct);

        return id;
    }

    public Task RecordStepAsync(LifecycleAuditStepEntry step, CancellationToken ct)
    {
        return _steps.AddAsync(new LifecycleStepRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            LifecycleEventId = step.LifecycleEventId,
            StepName = step.StepName,
            Sequence = step.Sequence,
            Status = step.Status,
            StartedUtc = step.StartedUtc,
            CompletedUtc = step.CompletedUtc,
            ErrorCode = step.ErrorCode,
            ErrorDetails = Trim(step.ErrorDetails, 2048),
            CorrelationId = step.CorrelationId
        }, ct);
    }

    private string? Serialize(object? value)
    {
        if (value is null)
            return null;

        return Trim(JsonSerializer.Serialize(value, _jsonOptions), 4000);
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
