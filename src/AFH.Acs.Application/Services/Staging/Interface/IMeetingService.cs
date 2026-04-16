using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Functions.Meetings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Services.Interface;

public interface IMeetingService
{

    Task<MeetingScheduleResponse> ScheduleAsync(
        MeetingScheduleRequest request,
        CancellationToken ct = default);

    Task<MeetingDetailsDto?> GetMeetingByIdAsync(
        string meetingId,
        CancellationToken ct = default);


    Task<MeetingDetailsDto?> GetMeetingByGroupIdAsync(
        string groupId,
        CancellationToken ct = default);

    Task<MeetingConsentResponse> RecordConsentAsync(
        string groupId,
        MeetingConsentRequest request,
        CancellationToken ct = default);


    Task<MeetingDetailsDto?> GetByGroupIdAsync(string groupId, CancellationToken ct = default);

    Task<JoinTokenResponse> IssueJoinTokenAsync(
        string groupId,
        JoinTokenRequest request,
        CancellationToken ct = default);
}

