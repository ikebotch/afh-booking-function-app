using System.Security.Claims;

namespace AFH.Booking.Application.Abstractions.Auth;

public interface ICurrentUserProfileResolver
{
    CurrentUserProfile Resolve(ClaimsPrincipal principal);
}
