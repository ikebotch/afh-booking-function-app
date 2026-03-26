namespace AFH.Booking.Application.Abstractions.Auth;

public interface IEntraTokenValidator
{
    Task<DomainUserTokenValidationResult> ValidateAsync(string token, CancellationToken ct);
}

public sealed class DomainUserTokenValidationResult
{
    public bool IsSuccess { get; init; }
    public System.Security.Claims.ClaimsPrincipal? Principal { get; init; }
    public string? ErrorMessage { get; init; }
    public string ErrorCode { get; init; } = "Unauthorized";

    public static DomainUserTokenValidationResult Success(System.Security.Claims.ClaimsPrincipal principal) =>
        new() { IsSuccess = true, Principal = principal };

    public static DomainUserTokenValidationResult Fail(string message, string errorCode = "Unauthorized") =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorCode = errorCode };
}
