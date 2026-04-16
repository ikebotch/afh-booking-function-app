namespace AFH.Acs.Domain.Entities;

public sealed class IssuedIdentityToken
{
    public string IdentityId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresOn { get; init; }
}
