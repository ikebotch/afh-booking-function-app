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

        if (!string.IsNullOrWhiteSpace(_options.ProviderName) &&
            !string.Equals(_options.ProviderName, "Composed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Email provider '{_options.ProviderName}' is configured, but queued notification email delivery has no production provider adapter wired.");
        }

        var providerMessageId = Guid.NewGuid().ToString("N")[..20];

        _logger.LogWarning(
            "Queued notification email gateway is composed-only and does not send production email. Recipient={Recipient} CorrelationId={CorrelationId} Subject={Subject} TextLength={TextLength}",
            request.Recipient.Email,
            request.CorrelationId,
            request.Subject,
            request.TextBody.Length);

        return Task.FromResult(new NotificationDeliveryResult("NonProductionComposed", providerMessageId));
    }
}
