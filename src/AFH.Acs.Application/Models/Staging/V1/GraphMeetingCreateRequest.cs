namespace AFH.Acs.Recorder.Models;

public sealed class GraphMeetingCreateRequest
{
    public string AdviserEmail { get; init; } = default!;
    public string AdviserName { get; init; } = default!;
    public string ClientEmail { get; init; } = default!;
    public string ClientName { get; init; } = default!;

    public string Subject { get; init; } = "AFH Client Meeting";
    public string? BodyHtml { get; init; }

    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public string TimeZone { get; init; } = "Europe/London";

    public string? Location { get; init; } = "Online – AFH Meeting";
    public string JoinUrl { get; init; } = default!;
}