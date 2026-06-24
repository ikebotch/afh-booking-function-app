namespace AFH.Booking.Contracts.V1.Requests.Identity;

public sealed class IdentityUserPermissionMappingRequest
{
    public string? Permission { get; init; }
    public Guid? UserProfileId { get; init; }
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public bool? IsGranted { get; init; }
    public bool? IsEnabled { get; init; }
    public string? Reason { get; init; }
}
