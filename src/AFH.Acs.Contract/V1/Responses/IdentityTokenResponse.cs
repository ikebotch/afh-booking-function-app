namespace AFH.Acs.Contract.V1.Responses;

public sealed class IdentityTokenResponse
{
    public string IdentityId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresOn { get; init; }
}
