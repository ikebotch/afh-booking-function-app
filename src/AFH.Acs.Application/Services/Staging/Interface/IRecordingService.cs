using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Models.V1;

namespace AFH.Acs.Recorder.Services.Interface;

public interface IRecordingService
{

    Task<StartRecordingResult> StartAsync(
        StartRecordingRequest request,
        CancellationToken ct = default);


    Task StopAsync(
        StopRecordingRequest request,
        CancellationToken ct = default);


    Task<IReadOnlyList<MeetingRecordingDto>> ListAsync(
      string? meetingId,
      CancellationToken ct = default);




    Task<MeetingRecordingDto?> GetAsync(
          string recordingId,
          CancellationToken ct = default);
}