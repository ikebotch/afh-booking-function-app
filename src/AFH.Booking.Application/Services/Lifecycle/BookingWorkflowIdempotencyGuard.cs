using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Lifecycle;

namespace AFH.Booking.Application.Services.Lifecycle;

public sealed class BookingWorkflowIdempotencyGuard : IBookingWorkflowIdempotencyGuard
{
    private readonly ILifecycleEventRepository _events;

    public BookingWorkflowIdempotencyGuard(ILifecycleEventRepository events)
    {
        _events = events;
    }

    public Task<LifecycleEventRecord?> FindCompletedAsync(string workflowKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workflowKey))
            return Task.FromResult<LifecycleEventRecord?>(null);

        return _events.FindLatestByTriggerReasonAsync(workflowKey.Trim(), ct);
    }
}
