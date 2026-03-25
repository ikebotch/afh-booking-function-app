namespace AFH.Booking.Application.Abstractions.Persistence;

public interface ILifecycleStepRepository
{
    Task AddAsync(LifecycleStepRecord record, CancellationToken ct);
}
