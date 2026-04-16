using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Acs.Recorder.Services.Interface;
public interface IMeetingAtrService
{

    Task<MeetingAtrAnalysisDto?> GetAnalysisAsync(string meetingId, CancellationToken ct = default);
    Task CreateAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default);


    Task UpdateAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default);

    Task<bool> DeleteAsync(string meetingId, CancellationToken ct = default);
}