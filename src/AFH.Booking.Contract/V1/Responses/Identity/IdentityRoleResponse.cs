namespace AFH.Booking.Contracts.V1.Responses.Identity;

public sealed class IdentityRoleResponse
{
    public Guid RoleId { get; init; }
    public string Role { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
