using AFH.Booking.Application.Models.Auth;

namespace AFH.Booking.Application.Abstractions.Auth;

public interface IEntraTokenValidator
{
    Task<DomainUserTokenValidationResult> ValidateAsync(string token, CancellationToken ct);
}
