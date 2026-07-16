namespace AFH.Booking.Domain;

public sealed record EndpointAccessRequirement
{
    public EndpointAccessRequirement(
        EndpointAccessPolicy policy,
        string? requiredPermission = null)
        : this(
            policy,
            string.IsNullOrWhiteSpace(requiredPermission)
                ? []
                : [requiredPermission])
    {
    }

    public EndpointAccessRequirement(
        EndpointAccessPolicy policy,
        IReadOnlyList<string> requiredPermissions)
    {
        Policy = policy;
        RequiredPermissions = requiredPermissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        RequiredPermission = RequiredPermissions.Count == 1 ? RequiredPermissions[0] : null;
    }

    public EndpointAccessPolicy Policy { get; init; }
    public string? RequiredPermission { get; init; }
    public IReadOnlyList<string> RequiredPermissions { get; init; }
    public string? RequiredPermissionDisplay => RequiredPermissions.Count == 0
        ? null
        : string.Join(" OR ", RequiredPermissions);
}
