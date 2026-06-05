using AFH.Booking.Domain;
using AFH.Booking.Domain.Auth;

namespace AFH.Booking.Function.Security;

public static class EndpointAccessPolicies
{
    private static readonly IReadOnlyDictionary<string, EndpointAccessRequirement> Requirements =
        new Dictionary<string, EndpointAccessRequirement>(StringComparer.Ordinal)
        {
            ["Admin_GetAdviserCoverage"] = InternalOnly(),
            ["Admin_GetAdviserProjectionById"] = InternalOnly(),
            ["Admin_GetAdviserProjectionFeed"] = InternalOnly(),
            ["Admin_ReconcileDownstreamUpdates"] = InternalOnly(),
            ["Admin_SyncAdviserDirectoryProjection"] = InternalOnly(),
            ["Approvals_ListPending"] = UserPermission(BookingPermissionNames.ApprovalsRead),
            ["Approvals_Review"] = UserPermission(BookingPermissionNames.ApprovalsReview),
            ["Approvals_ListAdviserRequests"] = UserPermission(BookingPermissionNames.ApprovalRequestsReadOwn),
            ["Booking_OpenApiV1"] = Public(),
            ["Booking_ScalarUi"] = Public(),
            ["EmailBouncebackFunctionV1"] = Public(),
            ["Bookings_CancelBooking"] = UserPermission(BookingPermissionNames.CancelDirect),
            ["Bookings_ConfirmHold"] = Public(),
            ["Bookings_CreateApprovalRequest"] = UserPermission(BookingPermissionNames.ApprovalRequestsCreate),
            ["Config_DeleteMeetingType"] = InternalOnly(),
            ["Config_DeleteMeetingTopic"] = InternalOnly(),
            ["Config_GetMeetingTypes"] = Public(),
            ["Config_GetMeetingTopics"] = Public(),
            ["Config_UpsertMeetingType"] = InternalOnly(),
            ["Config_UpsertMeetingTopic"] = InternalOnly(),
            ["Bookings_CreateHold"] = Public(),
            ["Bookings_GetBooking"] = Public(),
            ["Bookings_GetRearrangementOptions"] = InternalOnly(),
            ["Bookings_LeadTechCancel"] = UserPermission(BookingPermissionNames.CancelAsLeadTech),
            ["Bookings_LeadTechRearrange"] = UserPermission(BookingPermissionNames.RearrangeAsLeadTech),
            ["Bookings_LeadTechRearrangementOptions"] = UserPermission(BookingPermissionNames.RearrangementOptionsRead),
            ["Bookings_Rearrange"] = UserPermission(BookingPermissionNames.RearrangeDirect),
            ["Bookings_RecordEmailBounce"] = InternalOnly(),
            ["Bookings_RecordNoShow"] = InternalOnly(),
            ["Bookings_ReleaseHold"] = InternalOnly(),
            ["Bookings_RemediateShowAs"] = InternalOnly(),
            ["Bookings_SendNotification"] = InternalOnly(),
            ["Bookings_SelfServiceCancel"] = Public(),
            ["Bookings_SelfServiceGetBooking"] = Public(),
            ["Bookings_SelfServiceRearrange"] = Public(),
            ["Bookings_SelfServiceRearrangementOptions"] = Public(),
            ["CalendarHealthV1"] = Public(),
            ["Client_GetByTransaction"] = Public(),
            ["Client_GetByTransaction_V2"] = InternalOnly(),
            ["Clients_CreateDuplicateCase"] = InternalOnly(),
            ["Clients_ListDuplicateCases"] = InternalOnly(),
            ["Clients_ResolveDuplicateCase"] = InternalOnly(),
            ["Notifications_Dispatches_Get"] = InternalOnly(),
            ["Notifications_MessageLogs_Get"] = InternalOnly(),
            ["Notifications_RequestHttpV1"] = Public(),
            ["Notifications_Requests_DeadLetter"] = InternalOnly(),
            ["Notifications_Requests_Get"] = InternalOnly(),
            ["Notifications_Requests_List"] = InternalOnly(),
            ["Notifications_Requests_MarkFailed"] = InternalOnly(),
            ["Notifications_Requests_Requeue"] = InternalOnly(),
            ["Notifications_Templates_Activate"] = InternalOnly(),
            ["Notifications_Templates_Create"] = InternalOnly(),
            ["Notifications_Templates_Deactivate"] = InternalOnly(),
            ["Notifications_Templates_Get"] = InternalOnly(),
            ["Notifications_Templates_GetByKey"] = InternalOnly(),
            ["Notifications_Templates_List"] = InternalOnly(),
            ["Notifications_Templates_Preview"] = InternalOnly(),
            ["Notifications_Templates_Update"] = InternalOnly(),
            ["Transactions_Availability"] = Public(),
            ["Transactions_Availability_V2"] = InternalOnly(),
            ["Users_GetCurrentUser"] = UserAuthenticated(),

        };

    internal static IReadOnlyCollection<string> KnownHttpFunctions => Requirements.Keys.ToArray();

    public static EndpointAccessPolicy GetPolicy(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        return GetRequirement(functionName).Policy;
    }

    public static EndpointAccessRequirement GetRequirement(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        if (Requirements.TryGetValue(functionName, out var requirement))
            return requirement;

        throw new InvalidOperationException($"No endpoint access policy is configured for HTTP function '{functionName}'.");
    }

    private static EndpointAccessRequirement Public() => new(EndpointAccessPolicy.Public);
    private static EndpointAccessRequirement InternalOnly() => new(EndpointAccessPolicy.InternalOnly);
    private static EndpointAccessRequirement UserAuthenticated() => new(EndpointAccessPolicy.UserAuthenticated);
    private static EndpointAccessRequirement UserPermission(string permission) => new(EndpointAccessPolicy.UserAuthenticated, permission);
}
