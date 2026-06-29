namespace AFH.Booking.Domain.Auth;

public static class DomainUserCapabilities
{
    public const string BookingCancelDirect = "booking.cancel.direct";
    public const string BookingRearrangeDirect = "booking.rearrange.direct";
    public const string BookingChangeRequest = "booking.change.request";
    public const string BookingChangeApprove = "booking.change.approve";
    public const string BookingViewAuditSummary = "booking.view.audit.summary";
    public const string BookingAdmin = "booking.admin";

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles)
    {
        var capabilitySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            if (string.Equals(role, DomainUserRoles.Adviser, StringComparison.OrdinalIgnoreCase))
            {
                capabilitySet.Add(BookingChangeRequest);
            }
            else if (string.Equals(role, DomainUserRoles.Partner, StringComparison.OrdinalIgnoreCase))
            {
                capabilitySet.Add(BookingCancelDirect);
                capabilitySet.Add(BookingRearrangeDirect);
            }
            else if (string.Equals(role, DomainUserRoles.Manager, StringComparison.OrdinalIgnoreCase))
            {
                capabilitySet.Add(BookingChangeApprove);
                capabilitySet.Add(BookingViewAuditSummary);
            }
            else if (string.Equals(role, DomainUserRoles.Operations, StringComparison.OrdinalIgnoreCase))
            {
                capabilitySet.Add(BookingCancelDirect);
                capabilitySet.Add(BookingRearrangeDirect);
                capabilitySet.Add(BookingViewAuditSummary);
            }
            else if (string.Equals(role, DomainUserRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                capabilitySet.Add(BookingCancelDirect);
                capabilitySet.Add(BookingRearrangeDirect);
                capabilitySet.Add(BookingChangeRequest);
                capabilitySet.Add(BookingChangeApprove);
                capabilitySet.Add(BookingViewAuditSummary);
                capabilitySet.Add(BookingAdmin);
            }
        }

        return capabilitySet.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
