using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Integrations.XPlan.Profiles.CaseManagers;

public static class RecordingProfile
{
    public static RecordingTranscriptionRequest ToCreateDto(this MeetingRecordingEntity t)
    {
        var meeting = t.Meeting;
        var client = meeting.Lead;
        var adviser = meeting.Adviser;
       return new RecordingTranscriptionRequest
        {
            RecordingId = t.RecordingId,
            MeetingId = t.MeetingId,
            GroupId = t.GroupId,
            BlobName = t.BlobName,
            BlobUrl = t.BlobUrl,
            ClientName = client?.ClientName,
            MeetingDate = meeting.StartUtc,
            ClientEntityID = meeting?.LeadId,
            MeetingType = meeting?.MeetingType,
            AdviserEmail = adviser?.Email
        };
    }


    public static IEnumerable<MeetingRecordingDto> ToListDto(
        this IEnumerable<MeetingRecordingDto> recordings,
        IEnumerable<RecordingTranscriptionRequest> transcriptions)
    {
        return recordings.GroupJoin(
            transcriptions,
            r => r.RecordingId,
            t => t.RecordingId,
            (r, ts) =>
            {
                var transcription = ts.FirstOrDefault(); // or LastOrDefault, or handle list
                return new MeetingRecordingDto
                {
                    RecordingId = r.RecordingId,
                    MeetingId = r.MeetingId,
                    GroupId = r.GroupId,
                    BlobName = r.BlobName,
                    BlobUrl = r.BlobUrl,
                    ClientName = r.ClientName,
                    MeetingDate = r.MeetingDate,
                    ClientEntityID = r.ClientEntityID,
                    MeetingType = r.MeetingType,
                    AdviserEmail = r.AdviserEmail,
                    AdviserName = r.AdviserName,
                    MeetingTitle = r.MeetingTitle,
                    DurationSeconds = r.DurationSeconds,

                    Transcription = transcription?.Transcription,
                    FinancialGoals = transcription?.FinancialGoals,
                    Tax = transcription?.Tax,
                    AttitudetoRisk = transcription?.AttitudetoRisk
                };
            });
    }


}