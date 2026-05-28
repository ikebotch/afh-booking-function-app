using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Services.Lifecycle;

public sealed class BookingNotificationStep : IBookingNotificationStep
{
    private readonly INotificationPublisher _publisher;
    private readonly IBookingNotificationPolicyProvider _policyProvider;
    private readonly IBookingNotificationRecipientResolver _recipientResolver;
    private readonly ILogger<BookingNotificationStep> _logger;

    public BookingNotificationStep(
        INotificationPublisher publisher,
        IBookingNotificationPolicyProvider policyProvider,
        IBookingNotificationRecipientResolver recipientResolver,
        ILogger<BookingNotificationStep> logger)
    {
        _publisher = publisher;
        _policyProvider = policyProvider;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    public async Task<(string Status, string? ErrorCode, string? ErrorDetails)> ExecuteAsync(
        string lifecycleEventType,
        string correlationId,
        string actorType,
        IReadOnlyList<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Booking notification step started. LifecycleEventType={LifecycleEventType} CorrelationId={CorrelationId} RequestedRecipientCount={RecipientCount}",
            lifecycleEventType,
            correlationId,
            recipients.Count);

        var notificationType = MapEventType(lifecycleEventType);
        if (notificationType is null)
        {
            _logger.LogInformation(
                "Booking notification step skipped because lifecycle event has no notification mapping. LifecycleEventType={LifecycleEventType}",
                lifecycleEventType);
            return (LifecycleStepStatuses.Skipped, null, null);
        }

        _logger.LogInformation(
            "Booking notification step mapped lifecycle event. LifecycleEventType={LifecycleEventType} NotificationType={NotificationType}",
            lifecycleEventType,
            notificationType.Name);

        try
        {
            var policy = await _policyProvider.GetAsync("Booking", notificationType, ct);
            _logger.LogInformation(
                "Booking notification policy evaluated. NotificationType={NotificationType} PolicyEnabled={PolicyEnabled}",
                notificationType.Name,
                policy.Enabled);

            if (!policy.Enabled)
            {
                _logger.LogInformation(
                    "Booking notification skipped because policy is disabled. NotificationType={NotificationType}",
                    notificationType.Name);
                return (LifecycleStepStatuses.Skipped, null, null);
            }

            var resolvedRecipients = await _recipientResolver.ResolveAsync(policy, recipients, data, ct);
            _logger.LogInformation(
                "Booking notification recipients resolved. NotificationType={NotificationType} RecipientCount={RecipientCount}",
                notificationType.Name,
                resolvedRecipients.Count);

            if (resolvedRecipients.Count == 0)
            {
                _logger.LogInformation(
                    "Booking notification skipped because no recipients resolved. NotificationType={NotificationType}",
                    notificationType.Name);
                return (LifecycleStepStatuses.Skipped, null, null);
            }

            _logger.LogInformation(
                "Booking notification publish started. NotificationType={NotificationType} PublisherTransport={PublisherTransport}",
                notificationType.Name,
                _publisher.GetType().Name);

            await _publisher.PublishAsync(
                new NotificationRequested(
                    notificationType,
                    correlationId,
                    new NotificationActor(actorType, "Booking", null, null, null),
                    resolvedRecipients,
                    BuildPolicyData(data, policy)),
                ct);

            _logger.LogInformation(
                "Booking notification publish succeeded. NotificationType={NotificationType} PublisherTransport={PublisherTransport}",
                notificationType.Name,
                _publisher.GetType().Name);

            return (LifecycleStepStatuses.Succeeded, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Booking notification publish failed. LifecycleEventType={LifecycleEventType} NotificationType={NotificationType}",
                lifecycleEventType,
                notificationType.Name);
            return (LifecycleStepStatuses.Failed, LifecycleErrorCodes.NotificationFailed, ex.Message);
        }
    }

    private static NotificationType? MapEventType(string lifecycleEventType) => lifecycleEventType switch
    {
        LifecycleEventTypes.Booked => BookingNotificationTypes.BookingConfirmed,
        LifecycleEventTypes.Cancelled => BookingNotificationTypes.BookingCancelled,
        LifecycleEventTypes.Rearranged => BookingNotificationTypes.BookingRescheduled,
        LifecycleEventTypes.HoldCreated => BookingNotificationTypes.BookingHoldCreated,
        _ => null
    };

    private static IReadOnlyDictionary<string, string> BuildPolicyData(
        IReadOnlyDictionary<string, string> data,
        BookingNotificationPolicy policy)
    {
        var enriched = data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        foreach (var channel in policy.Channels.Where(x => x.Enabled))
        {
            enriched[$"TemplateKey:{channel.Channel}"] = channel.TemplateKey;
            enriched[$"TemplateVersion:{channel.Channel}"] = channel.TemplateVersion;
        }

        return enriched;
    }
}
