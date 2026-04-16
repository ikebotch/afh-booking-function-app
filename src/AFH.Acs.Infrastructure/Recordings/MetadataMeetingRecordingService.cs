using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Infrastructure.Persistence.Repositories;

namespace AFH.Acs.Infrastructure.Recordings;

public sealed class MetadataMeetingRecordingService(
    IMeetingSessionRepository sessions,
    IMeetingRecordingRepository recordings) : IMeetingRecordingService
{
    public async Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var meetingId = await ResolveMeetingIdAsync(request.MeetingId, request.GroupId, ct);
        var blobName = string.IsNullOrWhiteSpace(request.BlobName)
            ? $"recordings/{Guid.NewGuid():N}.wav"
            : request.BlobName.Trim();
        var blobUrl = $"https://localhost/recordings/{Guid.NewGuid():N}";

        var artifact = await recordings.StartAsync(meetingId, blobName, blobUrl, DateTimeOffset.UtcNow, ct);
        return await MapAsync(artifact, ct);
    }

    public async Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var artifact = await recordings.StopAsync(request.RecordingId, DateTimeOffset.UtcNow, ct)
            ?? throw new InvalidOperationException($"Recording not found for RecordingId={request.RecordingId}.");

        return await MapAsync(artifact, ct);
    }

    public async Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default)
    {
        var items = await recordings.ListAsync(meetingId, ct);
        var mapped = new List<MeetingRecordingResponse>(items.Count);
        foreach (var item in items)
        {
            mapped.Add(await MapAsync(item, ct));
        }

        return mapped;
    }

    public async Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default)
    {
        var artifact = await recordings.GetAsync(recordingId, ct);
        if (artifact is null)
        {
            return null;
        }

        return await MapAsync(artifact, ct);
    }

    private async Task<string> ResolveMeetingIdAsync(string? meetingId, string? groupId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(meetingId))
        {
            return meetingId.Trim();
        }

        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw new ArgumentException("meetingId or groupId is required.", nameof(meetingId));
        }

        var session = await sessions.GetByGroupIdAsync(groupId.Trim(), ct)
            ?? throw new InvalidOperationException($"Meeting not found for GroupId={groupId}.");

        return session.MeetingId;
    }

    private async Task<MeetingRecordingResponse> MapAsync(MeetingRecordingArtifact artifact, CancellationToken ct)
    {
        var session = string.IsNullOrWhiteSpace(artifact.MeetingId)
            ? null
            : await sessions.GetByIdAsync(artifact.MeetingId, ct);

        return new MeetingRecordingResponse
        {
            RecordingId = artifact.RecordingId,
            MeetingId = artifact.MeetingId,
            GroupId = session?.GroupId ?? string.Empty,
            BlobName = artifact.BlobName,
            BlobUrl = artifact.BlobUrl,
            RecordingStartUtc = artifact.RecordingStartUtc,
            RecordingEndUtc = artifact.RecordingEndUtc,
            DurationSeconds = artifact.DurationSeconds
        };
    }
}
