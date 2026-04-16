namespace AFH.Acs.Recorder.Models.V1;


public record ScheduleMeetingResponse(
    string MeetingId,
    string GroupId,
    string JoinUrl,
    string JoinCode,
    string GraphEventId,
    DateTimeOffset Start,
    DateTimeOffset End);