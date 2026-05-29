using AFH.Booking.Application.Models.Auth;

namespace AFH.Booking.Application.Abstractions.Auth;

public interface IAdviserUserContextClient
{
    Task<AdviserUserContext?> GetCurrentUserAsync(
        string bearerToken,
        CancellationToken ct);
}
