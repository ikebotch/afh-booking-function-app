namespace AFH.Booking.Contracts.V1.Requests.Identity;

public sealed class IdentityRoleUpsertRequest
{
    public string? Role { get; init; }
    public IReadOnlyList<string>? Permissions { get; init; }
}
