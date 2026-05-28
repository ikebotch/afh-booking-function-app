using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Infrastructure.Integration;

public sealed class InProcessNotificationPublisher : INotificationPublisher
{
    private readonly INotificationRequestIngestionService _ingestionService;

    public InProcessNotificationPublisher(INotificationRequestIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        await _ingestionService.AcceptAsync(notification, ct);
    }
}
