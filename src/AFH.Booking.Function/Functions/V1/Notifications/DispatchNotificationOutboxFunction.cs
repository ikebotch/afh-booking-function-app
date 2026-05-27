using AFH.Notification.Application.Options;
using AFH.Notification.Application.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Function.Functions.V1.Notifications;

public sealed class DispatchNotificationOutboxFunction
{
    private readonly NotificationOutboxDispatcher _dispatcher;
    private readonly NotificationOutboxDispatchOptions _options;
    private readonly ILogger<DispatchNotificationOutboxFunction> _logger;

    public DispatchNotificationOutboxFunction(
        NotificationOutboxDispatcher dispatcher,
        IOptions<NotificationOutboxDispatchOptions> options,
        ILogger<DispatchNotificationOutboxFunction> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    [Function(nameof(DispatchNotificationOutboxFunction))]
    public async Task RunAsync(
        [TimerTrigger("%Notifications:Outbox:DispatchSchedule%")] TimerInfo timer,
        FunctionContext context)
    {
        if (_options.IsAzureQueueMode)
        {
            _logger.LogInformation("SQL notification dispatch skipped because DispatcherMode=AzureQueue.");
            return;
        }

        var dispatched = await _dispatcher.DispatchDueBatchAsync(context.CancellationToken);
        _logger.LogInformation("SQL notification outbox dispatch completed. ClaimedCount={ClaimedCount}", dispatched);
    }
}
