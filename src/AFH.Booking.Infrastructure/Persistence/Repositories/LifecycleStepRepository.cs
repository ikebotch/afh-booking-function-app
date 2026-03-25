using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class LifecycleStepRepository : ILifecycleStepRepository
{
    private readonly BookingDbContext _db;

    public LifecycleStepRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(LifecycleStepRecord record, CancellationToken ct)
    {
        await _db.LifecycleSteps.AddAsync(new LifecycleStepModel
        {
            Id = record.Id,
            LifecycleEventId = record.LifecycleEventId,
            StepName = record.StepName,
            Sequence = record.Sequence,
            Status = record.Status,
            StartedUtc = record.StartedUtc,
            CompletedUtc = record.CompletedUtc,
            ErrorCode = record.ErrorCode,
            ErrorDetails = record.ErrorDetails,
            CorrelationId = record.CorrelationId
        }, ct);
    }
}
