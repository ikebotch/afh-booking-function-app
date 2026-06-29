namespace AFH.Booking.Application.Abstractions.Persistence;

public interface ILifecycleEventRepository
{
    Task AddAsync(LifecycleEventRecord record, CancellationToken ct);
    Task<LifecycleEventRecord?> FindLatestByTriggerReasonAsync(string triggerReason, CancellationToken ct);
    Task<IReadOnlyList<LifecycleEventRecord>> ListByBookingAsync(string bookingId, CancellationToken ct);
}
