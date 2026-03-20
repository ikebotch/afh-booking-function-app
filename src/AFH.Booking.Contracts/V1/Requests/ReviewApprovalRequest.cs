namespace AFH.Booking.Contracts.V1.Requests;

public sealed class ReviewApprovalRequest
{
    public bool Approved { get; init; }
    public string Reviewer { get; init; } = "Ian";
    public string? Notes { get; init; }
}
