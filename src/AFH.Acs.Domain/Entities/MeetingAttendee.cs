namespace AFH.Acs.Domain.Entities;

public sealed class MeetingAttendee
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string ResponseStatus { get; init; } = "None";
    public DateTimeOffset? ResponseTimeUtc { get; init; }
}
