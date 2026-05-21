namespace AFH.Booking.Application.Models.Auth;

public sealed class CurrentUserProfile
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
