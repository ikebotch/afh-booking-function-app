namespace AFH.Acs.Contract.V1.Responses;

public sealed class ScheduleMeetingResponse
{
    public string MeetingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string GraphEventId { get; init; } = string.Empty;
    public string ClientJoinUrl { get; init; } = string.Empty;
    public string AdviserJoinUrl { get; init; } = string.Empty;
    public string JoinCode { get; init; } = string.Empty;
    public string AdviserId { get; init; } = string.Empty;
    public string LeadId { get; init; } = string.Empty;
    public string MeetingType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string ClientEmail { get; init; } = string.Empty;
    public string? ClientName { get; init; }
}
