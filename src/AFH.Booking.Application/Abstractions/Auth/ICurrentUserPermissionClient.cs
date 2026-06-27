using AFH.Booking.Application.Models.Auth;

namespace AFH.Booking.Application.Abstractions.Auth;

public interface ICurrentUserPermissionClient
{
    Task<CurrentUserPermissionResult> GetCurrentUserAsync(
        string bearerToken,
        CancellationToken ct);

    Task<CurrentUserPermissionResult> AuthorizeAsync(
        string bearerToken,
        string requiredPermission,
        CancellationToken ct);
}
