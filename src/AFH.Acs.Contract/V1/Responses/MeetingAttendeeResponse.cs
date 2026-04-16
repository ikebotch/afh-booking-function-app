namespace AFH.Acs.Contract.V1.Responses;

public sealed class MeetingAttendeeResponse
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string ResponseStatus { get; init; } = "None";
    public DateTimeOffset? ResponseTimeUtc { get; init; }
}
