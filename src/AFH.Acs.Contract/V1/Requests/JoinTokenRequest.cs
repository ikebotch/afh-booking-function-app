namespace AFH.Acs.Contract.V1.Requests;

public sealed class JoinTokenRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = "Client";
}
