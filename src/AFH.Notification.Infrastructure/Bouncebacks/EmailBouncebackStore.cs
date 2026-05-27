using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Bouncebacks;

public sealed class EmailBouncebackStore : INotificationBouncebackStore
{
    private readonly ILogger<EmailBouncebackStore> _logger;

    public EmailBouncebackStore(ILogger<EmailBouncebackStore> logger)
    {
        _logger = logger;
    }

    public Task RecordBouncebackAsync(NotificationBounceback bounceback, CancellationToken ct)
    {
        _logger.LogInformation(
            "Bounceback recorded for ProviderMessageId={ProviderMessageId}, Status={Status}, Reason={Reason}, Timestamp={Timestamp}",
            bounceback.ProviderMessageId,
            bounceback.Status,
            bounceback.BounceReason,
            bounceback.TimestampUtc);

        return Task.CompletedTask;
    }
}
