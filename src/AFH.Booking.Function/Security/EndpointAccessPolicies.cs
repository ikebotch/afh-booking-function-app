using AFH.Booking.Domain;

namespace AFH.Booking.Function.Security;

public static class EndpointAccessPolicies
{
    private static readonly IReadOnlyDictionary<string, EndpointAccessPolicy> Policies =
        new Dictionary<string, EndpointAccessPolicy>(StringComparer.Ordinal)
        {
            ["Admin_GetAdviserCoverage"] = EndpointAccessPolicy.InternalOnly,
            ["Admin_GetAdviserProjectionById"] = EndpointAccessPolicy.InternalOnly,
            ["Admin_GetAdviserProjectionFeed"] = EndpointAccessPolicy.InternalOnly,
            ["Admin_ReconcileDownstreamUpdates"] = EndpointAccessPolicy.InternalOnly,
            ["Admin_SyncAdviserDirectoryProjection"] = EndpointAccessPolicy.InternalOnly,
            ["Approvals_ListPending"] = EndpointAccessPolicy.InternalOnly,
            ["Approvals_Review"] = EndpointAccessPolicy.InternalOnly,
            ["Booking_OpenApiV1"] = EndpointAccessPolicy.Public,
            ["Booking_ScalarUi"] = EndpointAccessPolicy.Public,
            ["EmailBouncebackFunctionV1"] = EndpointAccessPolicy.Public,
            ["Bookings_CancelBooking"] = EndpointAccessPolicy.Public,
            ["Bookings_ConfirmHold"] = EndpointAccessPolicy.Public,
            ["Bookings_CreateApprovalRequest"] = EndpointAccessPolicy.InternalOnly,
            ["Config_DeleteMeetingType"] = EndpointAccessPolicy.InternalOnly,
            ["Config_DeleteMeetingTopic"] = EndpointAccessPolicy.InternalOnly,
            ["Config_GetMeetingTypes"] = EndpointAccessPolicy.Public,
            ["Config_GetMeetingTopics"] = EndpointAccessPolicy.Public,
            ["Config_UpsertMeetingType"] = EndpointAccessPolicy.InternalOnly,
            ["Config_UpsertMeetingTopic"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_CreateHold"] = EndpointAccessPolicy.Public,
            ["Bookings_GetBooking"] = EndpointAccessPolicy.Public,
            ["Bookings_GetRearrangementOptions"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_LeadTechCancel"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_LeadTechRearrange"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_LeadTechRearrangementOptions"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_Rearrange"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_RecordEmailBounce"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_RecordNoShow"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_ReleaseHold"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_RemediateShowAs"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_SendNotification"] = EndpointAccessPolicy.InternalOnly,
            ["Bookings_SelfServiceCancel"] = EndpointAccessPolicy.Public,
            ["Bookings_SelfServiceGetBooking"] = EndpointAccessPolicy.Public,
            ["Bookings_SelfServiceRearrange"] = EndpointAccessPolicy.Public,
            ["Bookings_SelfServiceRearrangementOptions"] = EndpointAccessPolicy.Public,
            ["CalendarHealthV1"] = EndpointAccessPolicy.Public,
            ["Client_GetByTransaction"] = EndpointAccessPolicy.Public,
            ["Client_GetByTransaction_V2"] = EndpointAccessPolicy.InternalOnly,
            ["Clients_CreateDuplicateCase"] = EndpointAccessPolicy.InternalOnly,
            ["Clients_ListDuplicateCases"] = EndpointAccessPolicy.InternalOnly,
            ["Clients_ResolveDuplicateCase"] = EndpointAccessPolicy.InternalOnly,
            ["Notifications_RequestHttpV1"] = EndpointAccessPolicy.InternalOnly,
            ["Transactions_Availability"] = EndpointAccessPolicy.Public,
            ["Transactions_Availability_V2"] = EndpointAccessPolicy.InternalOnly,
            ["Users_GetCurrentUser"] = EndpointAccessPolicy.UserAuthenticated,

        };

    internal static IReadOnlyCollection<string> KnownHttpFunctions => Policies.Keys.ToArray();

    public static EndpointAccessPolicy GetPolicy(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        if (Policies.TryGetValue(functionName, out var policy))
            return policy;

        throw new InvalidOperationException($"No endpoint access policy is configured for HTTP function '{functionName}'.");
    }
}
