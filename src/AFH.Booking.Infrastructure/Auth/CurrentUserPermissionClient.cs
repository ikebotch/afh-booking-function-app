using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Application.Models.Auth;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class CurrentUserPermissionClient : ICurrentUserPermissionClient
{
    private readonly IAdviserUserContextClient _adviserUserContext;
    private string? _cachedBearerToken;
    private Task<AdviserUserContext?>? _cachedUserContext;

    public CurrentUserPermissionClient(IAdviserUserContextClient adviserUserContext)
    {
        _adviserUserContext = adviserUserContext;
    }

    public async Task<CurrentUserPermissionResult> AuthorizeAsync(
        string bearerToken,
        string requiredPermission,
        CancellationToken ct)
    {
        var user = await ResolveCurrentUserAsync(bearerToken, ct);
        if (user is null)
            return CurrentUserPermissionResult.Unavailable("Unable to resolve current user permissions.");

        return user.Permissions.Contains(requiredPermission, StringComparer.OrdinalIgnoreCase)
            ? CurrentUserPermissionResult.Authorised(user)
            : CurrentUserPermissionResult.Forbidden(user, requiredPermission);
    }

    public async Task<CurrentUserPermissionResult> GetCurrentUserAsync(
        string bearerToken,
        CancellationToken ct)
    {
        var user = await ResolveCurrentUserAsync(bearerToken, ct);
        return user is null
            ? CurrentUserPermissionResult.Unavailable("Unable to resolve current user permissions.")
            : CurrentUserPermissionResult.Authorised(user);
    }

    private Task<AdviserUserContext?> ResolveCurrentUserAsync(string bearerToken, CancellationToken ct)
    {
        if (_cachedUserContext is not null && string.Equals(_cachedBearerToken, bearerToken, StringComparison.Ordinal))
            return _cachedUserContext;

        _cachedBearerToken = bearerToken;
        _cachedUserContext = _adviserUserContext.GetCurrentUserAsync(bearerToken, ct);
        return _cachedUserContext;
    }
}
