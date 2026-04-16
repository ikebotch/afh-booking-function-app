namespace AFH.Acs.Recorder.Models.V1;
public record MeetingJoinResult(
    string MeetingId,
    string GroupId,
    string JoinUrl,
    string JoinCode,
    string UserId,
    string Token,
    DateTimeOffset ExpiresOn,
    string DisplayName);

public record JoinExistingRequest(string JoinCode, string DisplayName);