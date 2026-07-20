namespace AFH.Booking.Application.Models.Approvals;

public sealed class ApprovalRequestConflictException : Exception
{
    public ApprovalRequestConflictException(string message) : base(message)
    {
    }
}
