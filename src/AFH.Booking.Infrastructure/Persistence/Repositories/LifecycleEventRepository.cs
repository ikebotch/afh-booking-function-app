using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;

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
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            ReasonCode = record.ReasonCode,
            ReasonNotes = record.ReasonNotes,
            BeforeJson = record.BeforeJson,
            AfterJson = record.AfterJson,
            OccurredUtc = record.OccurredUtc,
            CorrelationId = record.CorrelationId,
            SourceSystem = record.SourceSystem,
            RelatedBookingId = record.RelatedBookingId
        }, ct);
    }
}
