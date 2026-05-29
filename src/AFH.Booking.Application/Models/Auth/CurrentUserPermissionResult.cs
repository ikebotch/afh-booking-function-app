namespace AFH.Booking.Application.Models.Auth;

public sealed class CurrentUserPermissionResult
{
    public bool IsAuthorised { get; init; }
    public AdviserUserContext? User { get; init; }
    public string? FailureMessage { get; init; }

    public static CurrentUserPermissionResult Authorised(AdviserUserContext user) =>
        new() { IsAuthorised = true, User = user };

    public static CurrentUserPermissionResult Forbidden(AdviserUserContext user, string requiredPermission) =>
        new()
        {
            IsAuthorised = false,
            User = user,
            FailureMessage = $"Permission '{requiredPermission}' is required."
        };

    public static CurrentUserPermissionResult Unavailable(string message) =>
        new() { IsAuthorised = false, FailureMessage = message };
}
