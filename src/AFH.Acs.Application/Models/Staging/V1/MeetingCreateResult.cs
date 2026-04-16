using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Models.V1;
public record MeetingCreateResult(
    string MeetingId,
    string GroupId,
    string JoinUrl,
    string JoinCode,
    string UserId,
    string Token,
    DateTimeOffset ExpiresOn,
    string? Title,
    string? DisplayName);

public record JoinRequest(string? DisplayName, string? Title, DateTimeOffset? ScheduledStart);