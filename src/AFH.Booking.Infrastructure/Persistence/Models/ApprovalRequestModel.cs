namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class ApprovalRequestModel
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string ChangeType { get; set; } = default!;
    public string RequestedBy { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public DateTime RequestedUtc { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public string? Reviewer { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public string? ReviewNotes { get; set; }
}
