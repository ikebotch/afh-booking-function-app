namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class ApprovalHistoryModel
{
    public string Id { get; set; } = default!;
    public string ApprovalRequestId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string ActorType { get; set; } = default!;
    public string? ActorId { get; set; }
    public string Outcome { get; set; } = default!;
    public string? Comments { get; set; }
    public DateTime OccurredUtc { get; set; }
}
