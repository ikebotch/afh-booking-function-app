namespace AFH.Booking.Domain.Auth;

public static class BookingPermissionNames
{
    public const string ApprovalsRead = "Bookings.Approvals.Read";
    public const string ApprovalsReview = "Bookings.Approvals.Review";
    public const string ApprovalRequestsCreate = "Bookings.ApprovalRequests.Create";
    public const string ApprovalRequestsReadOwn = "Bookings.ApprovalRequests.ReadOwn";
    public const string CancelAsLeadTech = "Bookings.Cancel.AsLeadTech";
    public const string CancelDirect = "Bookings.Cancel.Direct";
    public const string RearrangeAsLeadTech = "Bookings.Rearrange.AsLeadTech";
    public const string RearrangeDirect = "Bookings.Rearrange.Direct";
    public const string RearrangementOptionsRead = "Bookings.RearrangementOptions.Read";
    public const string AdminRead = "Bookings.Admin.Read";
}
