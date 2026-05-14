namespace AFH.Booking.Application.Abstractions.Lifecycle;

public sealed record LifecycleAuditEntry(
    string BookingId,
    string? TransactionId,
    string EventType,
    string? ActorType,
    string? ActorId,
    string? ReasonCode,
    string? ReasonNotes,
    object? Before,
    object? After,
    DateTime OccurredUtc,
    string? CorrelationId,
    string? SourceSystem = null,
    string? RelatedBookingId = null,
    string? PreviousState = null,
    string? NewState = null,
    string? TriggerReason = null);

public sealed record LifecycleAuditStepEntry(
    string LifecycleEventId,
    string StepName,
    int Sequence,
    string Status,
    DateTime StartedUtc,
    DateTime? CompletedUtc = null,
    string? ErrorCode = null,
    string? ErrorDetails = null,
    string? CorrelationId = null);

public sealed record NotificationDispatchRequest(
    string BookingId,
    string EventType,
    string? Message,
    bool SendSms,
    bool SendEmail,
    string? LifecycleEventId = null,
    string? CorrelationId = null);