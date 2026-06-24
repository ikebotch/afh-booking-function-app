namespace AFH.Booking.Contracts.V1.Requests.Identity;

public sealed class IdentityUserProfileUpsertRequest
{
    public string? ExternalSubject { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AdviserId { get; init; }
    public string? JobRole { get; init; }
    public string? Status { get; init; }
}
