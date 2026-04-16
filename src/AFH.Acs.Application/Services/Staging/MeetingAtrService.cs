using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AFH.Acs.Recorder.Services.Interface;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Recorder.Services;

public sealed class MeetingAtrService : IMeetingAtrService
{
    private readonly IMeetingAtrRepository _repo;
    private readonly ILogger<MeetingAtrService> _logger;

    public MeetingAtrService(
        IMeetingAtrRepository repo,
        ILogger<MeetingAtrService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<MeetingAtrAnalysisDto?> GetAnalysisAsync(string meetingId, CancellationToken ct = default)
        => _repo.GetAnalysisAsync(meetingId, ct);

    public async Task CreateAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default)
    {
        if (await _repo.ExistsAsync(entity.MeetingId, ct))
            throw new InvalidOperationException($"ATR record already exists for MeetingId={entity.MeetingId}");

        await _repo.InsertAsync(entity, ct);
    }

    public Task UpdateAsync(MeetingAtrAnalysisEntity entity, CancellationToken ct = default)
        => _repo.UpdateAsync(entity, ct);

    public Task<bool> DeleteAsync(string meetingId, CancellationToken ct = default)
        => _repo.DeleteAsync(meetingId, ct);
}