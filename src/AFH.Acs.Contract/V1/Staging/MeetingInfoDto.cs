namespace AFH.Acs.Recorder.DTOs;


public record MeetingInfoDto(
    string MeetingId,
    string GroupId,
    string AdviserId,
    string LeadId,
    string MeetingType,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string ClientEmail,
    bool ConsentToRecording
);