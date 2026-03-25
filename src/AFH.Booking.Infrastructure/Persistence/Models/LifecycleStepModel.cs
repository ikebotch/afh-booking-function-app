namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class LifecycleStepModel
{
    public string Id { get; set; } = default!;
    public string LifecycleEventId { get; set; } = default!;
    public LifecycleEventModel LifecycleEvent { get; set; } = default!;
    public string StepName { get; set; } = default!;
    public int Sequence { get; set; }
    public string Status { get; set; } = default!;
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetails { get; set; }
    public string? CorrelationId { get; set; }
}
