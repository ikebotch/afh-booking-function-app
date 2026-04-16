namespace AFH.Acs.Application.Models;

public sealed class ScheduleMeetingCommand
{
    public string AdviserId { get; init; } = string.Empty;
    public string LeadId { get; init; } = string.Empty;
    public string MeetingType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string ClientEmail { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
}
