namespace AFH.Acs.Recorder.Models.V1;

public record ScheduleMeetingRequest(
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string[] Attendees,
    string? Description,
    string? TimeZone = "UTC");