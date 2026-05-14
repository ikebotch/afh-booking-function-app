namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class ApprovalRequestModel
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string TransactionId { get; set; } = default!;
    public string ChangeType { get; set; } = default!;
    public string RequestedBy { get; set; } = default!;
    public string? RequesterId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime RequestedUtc { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public string? RequestedPayloadJson { get; set; }
    public string ApproverTargetType { get; set; } = default!;
    public string ApproverTargetValue { get; set; } = default!;
    public string ApproverTargetDisplayName { get; set; } = default!;
    public string? Reviewer { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ExecutedUtc { get; set; }
    public string? ExecutionError { get; set; }
}
