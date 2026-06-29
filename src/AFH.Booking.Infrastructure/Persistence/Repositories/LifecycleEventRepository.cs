using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class LifecycleEventRepository : ILifecycleEventRepository
{
    private readonly BookingDbContext _db;

    public LifecycleEventRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(LifecycleEventRecord record, CancellationToken ct)
    {
        await _db.LifecycleEvents.AddAsync(new LifecycleEventModel
        {
            Id = record.Id,
            BookingId = record.BookingId,
            TransactionId = record.TransactionId,
            EventType = record.EventType,
            PreviousState = record.PreviousState,
            NewState = record.NewState,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            ReasonCode = record.ReasonCode,
            ReasonNotes = record.ReasonNotes,
            BeforeJson = record.BeforeJson,
            AfterJson = record.AfterJson,
            OccurredUtc = record.OccurredUtc,
            CorrelationId = record.CorrelationId,
            SourceSystem = record.SourceSystem,
            PartnerName = record.PartnerName,
            RelatedBookingId = record.RelatedBookingId,
            TriggerReason = record.TriggerReason
        }, ct);
    }

    public async Task<LifecycleEventRecord?> FindLatestByTriggerReasonAsync(string triggerReason, CancellationToken ct)
    {
        var row = await _db.LifecycleEvents
            .AsNoTracking()
            .Where(x => x.TriggerReason == triggerReason)
            .OrderByDescending(x => x.OccurredUtc)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : Map(row);
    }

    public async Task<IReadOnlyList<LifecycleEventRecord>> ListByBookingAsync(string bookingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return [];

        var lookup = bookingId.Trim();
        var rows = await _db.LifecycleEvents
            .AsNoTracking()
            .Include(x => x.Steps)
            .Where(x => x.BookingId == lookup || x.RelatedBookingId == lookup)
            .OrderByDescending(x => x.OccurredUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    private static LifecycleEventRecord Map(LifecycleEventModel row)
        => new()
        {
            Id = row.Id,
            BookingId = row.BookingId,
            TransactionId = row.TransactionId,
            EventType = row.EventType,
            PreviousState = row.PreviousState,
            NewState = row.NewState,
            ActorType = row.ActorType,
            ActorId = row.ActorId,
            ReasonCode = row.ReasonCode,
            ReasonNotes = row.ReasonNotes,
            BeforeJson = row.BeforeJson,
            AfterJson = row.AfterJson,
            OccurredUtc = row.OccurredUtc,
            CorrelationId = row.CorrelationId,
            SourceSystem = row.SourceSystem,
            PartnerName = row.PartnerName,
            RelatedBookingId = row.RelatedBookingId,
            TriggerReason = row.TriggerReason,
            Steps = row.Steps
                .OrderBy(step => step.Sequence)
                .Select(step => new LifecycleStepRecord
                {
                    Id = step.Id,
                    LifecycleEventId = step.LifecycleEventId,
                    StepName = step.StepName,
                    Sequence = step.Sequence,
                    Status = step.Status,
                    StartedUtc = step.StartedUtc,
                    CompletedUtc = step.CompletedUtc,
                    ErrorCode = step.ErrorCode,
                    ErrorDetails = step.ErrorDetails,
                    CorrelationId = step.CorrelationId
                })
                .ToList()
        };
}
