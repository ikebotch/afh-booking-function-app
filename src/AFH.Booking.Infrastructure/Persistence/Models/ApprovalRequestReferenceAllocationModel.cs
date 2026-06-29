namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class ApprovalRequestReferenceAllocationModel
{
    public string Id { get; set; } = default!;
    public long Value { get; set; }
    public DateTime CreatedUtc { get; set; }
}
