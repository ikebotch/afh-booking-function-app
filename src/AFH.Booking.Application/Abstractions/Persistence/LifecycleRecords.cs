namespace AFH.Booking.Application.Abstractions.Persistence;

public sealed record LifecycleEventRecord
{
    public string Id { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string? TransactionId { get; init; }
    public string EventType { get; init; } = default!;
    public string? ActorType { get; init; }
    public string? ActorId { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonNotes { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime OccurredUtc { get; init; }
    public string? CorrelationId { get; init; }
    public string? SourceSystem { get; init; }
    public string? RelatedBookingId { get; init; }
}

public sealed record LifecycleStepRecord
{
    public string Id { get; init; } = default!;
    public string LifecycleEventId { get; init; } = default!;
    public string StepName { get; init; } = default!;
    public int Sequence { get; init; }
    public string Status { get; init; } = default!;
    public DateTime StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetails { get; init; }
    public string? CorrelationId { get; init; }
}
