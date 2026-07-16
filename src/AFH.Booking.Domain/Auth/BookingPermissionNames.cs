namespace AFH.Booking.Domain.Auth;

public static class BookingPermissionNames
{
    public const string ApprovalsRead = "Bookings.Approvals.Read";
    public const string ApprovalsReview = "Bookings.Approvals.Review";
    public const string ApprovalRequestsCreate = "Bookings.ApprovalRequests.Create";
    public const string ApprovalRequestsReadOwn = "Bookings.ApprovalRequests.ReadOwn";
    public const string CancelAsPartner = "Bookings.Cancel.AsPartner";
    public const string CancelDirect = "Bookings.Cancel.Direct";
    public const string RearrangeAsPartner = "Bookings.Rearrange.AsPartner";
    public const string RearrangeDirect = "Bookings.Rearrange.Direct";
    public const string RearrangementOptionsRead = "Bookings.RearrangementOptions.Read";
    public const string AdminRead = "Bookings.Admin.Read";
    public const string OwnRead = "Bookings.Own.Read";
    public const string NotificationsRead = "Notifications.Admin.Read";
    public const string NotificationsManage = "Notifications.Admin.Manage";
    public const string ReportsRead = "Bookings.Reports.Read";
    public const string SystemRead = "System.Admin.Read";
    public const string SystemManage = "System.Admin.Manage";
}
