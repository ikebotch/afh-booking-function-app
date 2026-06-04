using AFH.Booking.Application.Models.Lifecycle;

namespace AFH.Booking.Application.Abstractions.Lifecycle;

public interface IBookingWorkflowIdempotencyGuard
{
    Task<LifecycleEventRecord?> FindCompletedAsync(string workflowKey, CancellationToken ct);
}
