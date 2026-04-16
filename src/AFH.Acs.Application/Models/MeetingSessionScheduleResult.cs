namespace AFH.Acs.Application.Models;

public sealed class MeetingSessionScheduleResult
{
    public string MeetingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string CalendarEventReference { get; init; } = string.Empty;
    public string ClientJoinUrl { get; init; } = string.Empty;
    public string AdviserJoinUrl { get; init; } = string.Empty;
    public string JoinCode { get; init; } = string.Empty;
}
