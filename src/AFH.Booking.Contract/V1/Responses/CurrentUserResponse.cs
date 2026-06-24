namespace AFH.Booking.Contracts.V1.Responses;

public sealed class CurrentUserResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AdviserId { get; init; }
    public string? JobRole { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
