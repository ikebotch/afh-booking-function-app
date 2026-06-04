using AFH.Booking.Application.Models.Lifecycle;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface IBookingWorkflowIdempotencyGuard
{
    // Lookup-only guard: this finds a previously completed lifecycle outcome by workflow key.
    // It is not a distributed lock and must be paired with workflow state checks/row-version handling.
    Task<LifecycleEventRecord?> FindCompletedAsync(string workflowKey, CancellationToken ct);
}
