namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface ILifecycleAuditService
{
    Task<string> RecordEventAsync(LifecycleAuditEntry entry, CancellationToken ct);
    Task RecordStepAsync(LifecycleAuditStepEntry step, CancellationToken ct);
}
