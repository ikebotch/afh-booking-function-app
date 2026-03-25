namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class LifecycleEventModel
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string? TransactionId { get; set; }
    public string EventType { get; set; } = default!;
    public string? ActorType { get; set; }
    public string? ActorId { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonNotes { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string? CorrelationId { get; set; }
    public string? SourceSystem { get; set; }
    public string? RelatedBookingId { get; set; }

    public List<LifecycleStepModel> Steps { get; set; } = new();
}
