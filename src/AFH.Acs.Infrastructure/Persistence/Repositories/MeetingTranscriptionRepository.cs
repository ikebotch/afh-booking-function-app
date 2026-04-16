using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Infrastructure.Persistence.Repositories;

public sealed class MeetingTranscriptionRepository(MeetingDbContext dbContext) : IMeetingTranscriptionRepository
{
    public async Task AttachJobAsync(string meetingId, MeetingTranscriptionArtifact transcription, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            throw new ArgumentException("meetingId is required.", nameof(meetingId));

        ArgumentNullException.ThrowIfNull(transcription);

        var meeting = await dbContext.Meetings.FirstOrDefaultAsync(x => x.MeetingId == meetingId, ct)
            ?? throw new InvalidOperationException($"Meeting not found for MeetingId={meetingId}.");

        var entity = await dbContext.MeetingTranscriptions.FirstOrDefaultAsync(x => x.MeetingId == meeting.MeetingId, ct);
        if (entity is null)
        {
            entity = new MeetingTranscriptionEntity
            {
                TranscriptionId = transcription.TranscriptionId,
                MeetingId = meeting.MeetingId,
                Language = transcription.Language,
                FullText = transcription.FullText,
                SummaryText = transcription.SummaryText
            };
            await dbContext.MeetingTranscriptions.AddAsync(entity, ct);
        }
        else
        {
            entity.TranscriptionId = transcription.TranscriptionId;
            entity.Language = transcription.Language;
            entity.FullText = transcription.FullText;
            entity.SummaryText = transcription.SummaryText;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<MeetingTranscriptionArtifact?> GetByTranscriptionIdAsync(string transcriptionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptionId))
            return null;

        var entity = await dbContext.MeetingTranscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.TranscriptionId == transcriptionId, ct);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AttachContentAsync(string transcriptionId, string fullText, string? summaryText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptionId))
            throw new ArgumentException("transcriptionId is required.", nameof(transcriptionId));

        var entity = await dbContext.MeetingTranscriptions.FirstOrDefaultAsync(x => x.TranscriptionId == transcriptionId, ct)
            ?? throw new InvalidOperationException($"Transcription not found for TranscriptionId={transcriptionId}.");

        entity.FullText = fullText ?? string.Empty;
        entity.SummaryText = summaryText;
        await dbContext.SaveChangesAsync(ct);
    }

    private static MeetingTranscriptionArtifact ToDomain(MeetingTranscriptionEntity entity)
        => new()
        {
            TranscriptionId = entity.TranscriptionId,
            Language = entity.Language,
            FullText = entity.FullText,
            SummaryText = entity.SummaryText
        };
}
