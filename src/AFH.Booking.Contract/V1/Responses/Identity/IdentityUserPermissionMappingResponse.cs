namespace AFH.Booking.Contracts.V1.Responses.Identity;

public sealed class IdentityUserPermissionMappingResponse
{
    public Guid MappingId { get; init; }
    public Guid? UserProfileId { get; init; }
    public Guid PermissionId { get; init; }
    public string Permission { get; init; } = string.Empty;
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public bool IsGranted { get; init; }
    public bool IsEnabled { get; init; }
    public string? Reason { get; init; }
}
