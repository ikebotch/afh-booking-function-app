using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Delivery.Sms;

public sealed class SmsNotificationDeliveryGateway : INotificationDeliveryGateway
{
    private readonly SmsDeliveryOptions _options;
    private readonly ISmsProviderSender? _sender;
    private readonly ILogger<SmsNotificationDeliveryGateway> _logger;

    public SmsNotificationDeliveryGateway(
        IOptions<SmsDeliveryOptions> options,
        IEnumerable<ISmsProviderSender> senders,
        ILogger<SmsNotificationDeliveryGateway> logger)
    {
        _options = options.Value;
        _sender = senders.FirstOrDefault();
        _logger = logger;
    }

    public bool CanSend(NotificationChannel channel)
        => channel == NotificationChannel.Sms;

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
    {
        if (!_options.Enabled)
            return new NotificationDeliveryResult("ConfiguredOff", null, ResolveProviderName());

        if (!SmsPhoneNumber.TryNormalize(request.Recipient.MobileNumber, out var mobile))
            return new NotificationDeliveryResult("Skipped", null, ResolveProviderName());

        if (string.IsNullOrWhiteSpace(request.TextBody))
            return new NotificationDeliveryResult("Failed", null, ResolveProviderName(), "SMS body is required.");

        if (_sender is null)
        {
            var providerMessageId = $"sms-composed-{Guid.NewGuid():N}";
            _logger.LogWarning(
                "Queued notification SMS gateway is composed-only and does not send production SMS. RecipientMobile={RecipientMobile} CorrelationId={CorrelationId} TextLength={TextLength}",
                mobile,
                request.CorrelationId,
                request.TextBody.Length);

            return new NotificationDeliveryResult("NonProductionComposed", providerMessageId, "Composed");
        }

        try
        {
            return await _sender.SendAsync(request with { Recipient = request.Recipient with { MobileNumber = mobile } }, ct);
        }
        catch (Exception ex)
        {
            return new NotificationDeliveryResult("Failed", null, ResolveProviderName(), ex.Message);
        }
    }

    private string ResolveProviderName()
        => string.IsNullOrWhiteSpace(_options.ProviderName)
            ? "Composed"
            : _options.ProviderName.Trim();
}
