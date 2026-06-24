namespace AFH.Booking.Contracts.V1.Requests.Identity;

public sealed class IdentityUserRoleMappingRequest
{
    public string? Role { get; init; }
    public Guid? UserProfileId { get; init; }
    public string? Email { get; init; }
    public string? ExternalRole { get; init; }
    public string? ExternalGroupId { get; init; }
    public bool? IsEnabled { get; init; }
}
