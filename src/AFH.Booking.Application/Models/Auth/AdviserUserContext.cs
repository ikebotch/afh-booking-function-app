namespace AFH.Booking.Application.Models.Auth;

public sealed class AdviserUserContext
{
    public string UserId { get; init; } = string.Empty;
    public string ExternalSubject { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AdviserId { get; init; }
    public string? JobRole { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyList<AdviserUserAccessScope> AccessScopes { get; init; } = [];
}

public sealed class AdviserUserAccessScope
{
    public string Area { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string? ScopeValue { get; init; }
    public string? DisplayName { get; init; }
}
