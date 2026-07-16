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
            ["Admin_Reporting_Exports_Create"] = UserPermission(BookingPermissionNames.ReportsRead),
            ["Admin_Reporting_Exports_Download"] = UserPermission(BookingPermissionNames.ReportsRead),
            ["Admin_Reporting_Exports_List"] = UserPermission(BookingPermissionNames.ReportsRead),
            ["Admin_Reporting_ReportCatalogue"] = UserPermission(BookingPermissionNames.ReportsRead),
            ["Admin_Reports_BookingSummary"] = UserPermission(BookingPermissionNames.ReportsRead),
            ["Admin_ReconcileDownstreamUpdates"] = InternalOnly(),
            ["Admin_SyncAdviserDirectoryProjection"] = InternalOnly(),
            ["Admin_System_Audit"] = UserPermission(BookingPermissionNames.SystemRead),
            ["Admin_System_AuditLog"] = UserPermission(BookingPermissionNames.SystemRead),
            ["Admin_BookingPolicyDefaults_Get"] = UserPermission(BookingPermissionNames.SystemRead),
            ["Admin_BookingPolicyDefaults_Patch"] = UserPermission(BookingPermissionNames.SystemManage),
            ["Admin_System_FeatureFlags_Delete"] = UserPermission(BookingPermissionNames.SystemManage),
            ["Admin_System_FeatureFlags_List"] = UserPermission(BookingPermissionNames.SystemRead),
            ["Admin_System_FeatureFlags_Upsert"] = UserPermission(BookingPermissionNames.SystemManage),
            ["Admin_System_Health"] = UserPermission(BookingPermissionNames.SystemRead),
            ["Approvals_List"] = UserPermission(BookingPermissionNames.ApprovalsRead),
            ["Approvals_ListPending"] = UserPermission(BookingPermissionNames.ApprovalsRead),
            ["Approvals_Review"] = UserPermission(BookingPermissionNames.ApprovalsReview),
            ["Approvals_ListAdviserRequests"] = UserPermission(BookingPermissionNames.ApprovalRequestsReadOwn),
            ["Booking_OpenApiV1"] = Public(),
            ["Booking_ScalarUi"] = Public(),
            ["EmailBouncebackFunctionV1"] = Public(),
            ["Identity_ListUserProfiles"] = InternalOnly(),
            ["Identity_GetUserProfile"] = InternalOnly(),
            ["Identity_UpsertUserProfile"] = InternalOnly(),
            ["Identity_DeleteUserProfile"] = InternalOnly(),
            ["Identity_ListPermissions"] = InternalOnly(),
            ["Identity_UpsertPermission"] = InternalOnly(),
            ["Identity_DeletePermission"] = InternalOnly(),
            ["Identity_ListRoles"] = InternalOnly(),
            ["Identity_GetRole"] = InternalOnly(),
            ["Identity_UpsertRole"] = InternalOnly(),
            ["Identity_DeleteRole"] = InternalOnly(),
            ["Identity_AddRolePermission"] = InternalOnly(),
            ["Identity_RemoveRolePermission"] = InternalOnly(),
            ["Identity_ListUserRoleMappings"] = InternalOnly(),
            ["Identity_AssignUserRole"] = InternalOnly(),
            ["Identity_DeleteUserRoleMapping"] = InternalOnly(),
            ["Identity_ListUserPermissionMappings"] = InternalOnly(),
            ["Identity_AssignUserPermission"] = InternalOnly(),
            ["Identity_DeleteUserPermissionMapping"] = InternalOnly(),
            ["Bookings_CancelBooking"] = UserPermission(BookingPermissionNames.CancelDirect),
            ["Bookings_AdminSearch"] = UserPermissionAny(BookingPermissionNames.AdminRead, BookingPermissionNames.OwnRead),
            ["Bookings_ConfirmHold"] = Public(),
            ["Bookings_CreateApprovalRequest"] = UserPermission(BookingPermissionNames.ApprovalRequestsCreate),
            ["Config_DeleteMeetingType"] = InternalOnly(),
            ["Config_DeleteMeetingTopic"] = InternalOnly(),
            ["Config_GetMeetingTypes"] = Public(),
            ["Config_GetMeetingTopics"] = Public(),
            ["Config_UpsertMeetingType"] = InternalOnly(),
            ["Config_UpsertMeetingTopic"] = InternalOnly(),
            ["Bookings_CreateHold"] = Public(),
            ["Bookings_GetBooking"] = UserAuthenticated(),
            ["Bookings_GetBookingLifecycle"] = UserAuthenticated(),
            ["Bookings_GetRearrangementOptions"] = UserPermission(BookingPermissionNames.RearrangementOptionsRead),
            ["Bookings_PartnerCancel"] = UserPermission(BookingPermissionNames.CancelAsPartner),
            ["Bookings_PartnerRearrange"] = UserPermission(BookingPermissionNames.RearrangeAsPartner),
            ["Bookings_PartnerRearrangementOptions"] = UserPermission(BookingPermissionNames.RearrangementOptionsRead),
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
            ["Notifications_ChannelSettings_Create"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_ChannelSettings_List"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_ChannelSettings_Update"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Dispatches_Get"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_LifecycleEvents_List"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_MessageLogs_Get"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_RequestHttpV1"] = Public(),
            ["Notifications_Requests_DeadLetter"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Requests_Get"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Requests_List"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Requests_MarkFailed"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Requests_Requeue"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_RetryPolicies_Create"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_RetryPolicies_List"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_RetryPolicies_Update"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Settings_Delete"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Settings_Get"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Settings_List"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Settings_Upsert"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Templates_Activate"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Templates_Create"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Templates_Deactivate"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Templates_Get"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Templates_GetByKey"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Templates_List"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Templates_Preview"] = UserPermission(BookingPermissionNames.NotificationsRead),
            ["Notifications_Templates_TestSend"] = UserPermission(BookingPermissionNames.NotificationsManage),
            ["Notifications_Templates_Update"] = UserPermission(BookingPermissionNames.NotificationsManage),
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
    private static EndpointAccessRequirement UserPermissionAny(params string[] permissions) => new(EndpointAccessPolicy.UserAuthenticated, permissions);
}
