namespace AFH.Booking.Contracts.V1.Responses.Identity;

public sealed class IdentityUserProfileResponse
{
    public Guid UserProfileId { get; init; }
    public string ExternalSubject { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AdviserId { get; init; }
    public string? JobRole { get; init; }
    public string Status { get; init; } = string.Empty;
}
