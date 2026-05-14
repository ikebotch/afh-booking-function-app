using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class OperationalIssueRepository : IOperationalIssueRepository
{
    private readonly BookingDbContext _db;

    public OperationalIssueRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(OperationalIssueRecord record, CancellationToken ct)
    {
        await _db.Set<OperationalIssueModel>().AddAsync(Map(record), ct);
    }

    public async Task<OperationalIssueRecord?> GetLatestAsync(string adviserId, string providerEventId, string code, CancellationToken ct)
    {
        var model = await _db.Set<OperationalIssueModel>()
            .AsNoTracking()
            .Where(x => x.AdviserId == adviserId && x.ProviderEventId == providerEventId && x.Code == code)
            .OrderByDescending(x => x.DetectedUtc)
            .FirstOrDefaultAsync(ct);

        return model is null ? null : Map(model);
    }

    public Task<int> CountRecentAsync(string adviserId, string code, DateTime sinceUtc, CancellationToken ct)
    {
        return _db.Set<OperationalIssueModel>()
            .AsNoTracking()
            .Where(x => x.AdviserId == adviserId && x.Code == code && x.DetectedUtc >= sinceUtc)
            .CountAsync(ct);
    }

    public async Task UpdateAsync(OperationalIssueRecord record, CancellationToken ct)
    {
        var existing = await _db.Set<OperationalIssueModel>().FirstOrDefaultAsync(x => x.Id == record.Id, ct);
        if (existing is null)
            throw new InvalidOperationException($"Operational issue '{record.Id}' was not found.");

        existing.Status = record.Status;
        existing.MetadataJson = record.MetadataJson;
        existing.EscalationCount = record.EscalationCount;
        existing.LastEscalatedUtc = record.LastEscalatedUtc;
    }

    private static OperationalIssueModel Map(OperationalIssueRecord record)
        => new()
        {
            Id = record.Id,
            IssueType = record.IssueType,
            Code = record.Code,
            Severity = record.Severity,
            Status = record.Status,
            DetectedUtc = record.DetectedUtc,
            BookingId = record.BookingId,
            TransactionId = record.TransactionId,
            TransactionRef = record.TransactionRef,
            AdviserId = record.AdviserId,
            ProviderEventId = record.ProviderEventId,
            CorrelationId = record.CorrelationId,
            MetadataJson = record.MetadataJson,
            EscalationCount = record.EscalationCount,
            LastEscalatedUtc = record.LastEscalatedUtc
        };

    private static OperationalIssueRecord Map(OperationalIssueModel model)
        => new(
            model.Id,
            model.IssueType,
            model.Code,
            model.Severity,
            model.Status,
            model.DetectedUtc,
            model.BookingId,
            model.TransactionId,
            model.TransactionRef,
            model.AdviserId,
            model.ProviderEventId,
            model.CorrelationId,
            model.MetadataJson,
            model.EscalationCount,
            model.LastEscalatedUtc);
}
