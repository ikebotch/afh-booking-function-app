namespace AFH.Acs.Contract.V1.Responses;

public sealed class JoinTokenResponse
{
    public string MeetingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresOn { get; init; }
    public string? DisplayName { get; init; }
}
