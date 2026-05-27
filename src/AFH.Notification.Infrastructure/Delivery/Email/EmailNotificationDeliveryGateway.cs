using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Delivery.Email;

public sealed class EmailNotificationDeliveryGateway : INotificationDeliveryGateway
{
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<EmailNotificationDeliveryGateway> _logger;

    public EmailNotificationDeliveryGateway(
        IOptions<EmailDeliveryOptions> options,
        ILogger<EmailNotificationDeliveryGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool CanSend(NotificationChannel channel)
        => channel == NotificationChannel.Email;

    public Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
    {
        if (!_options.Enabled)
            return Task.FromResult(new NotificationDeliveryResult("ConfiguredOff", null));

        if (string.IsNullOrWhiteSpace(request.Recipient.Email))
            return Task.FromResult(new NotificationDeliveryResult("Skipped", null));

        var providerMessageId = Guid.NewGuid().ToString("N")[..20];

        _logger.LogInformation(
            "Composed notification email for {Recipient}. CorrelationId={CorrelationId} Subject={Subject} TextLength={TextLength}",
            request.Recipient.Email,
            request.CorrelationId,
            request.Subject,
            request.TextBody.Length);

        return Task.FromResult(new NotificationDeliveryResult("Composed", providerMessageId));
    }
}
