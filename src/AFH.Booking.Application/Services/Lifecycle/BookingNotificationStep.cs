using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Services.Lifecycle;

public sealed class BookingNotificationStep : IBookingNotificationStep
{
    private static readonly string[] ClientOnlyDataKeys =
    [
        "token",
        "selfServiceToken",
        "viewUrl",
        "cancelUrl",
        "rearrangeUrl",
        "rescheduleUrl",
        "bookingSelfServiceToken",
        "bookingChangeToken",
        "viewBookingUrl",
        "cancelBookingUrl",
        "rescheduleBookingUrl",
        "rearrangeBookingUrl",
        "manageBookingLink",
        "manageBookingLinks"
    ];

    private readonly IBookingNotificationPublisher _publisher;
    private readonly IBookingNotificationPolicyProvider _policyProvider;
    private readonly IBookingNotificationRecipientResolver _recipientResolver;
    private readonly ILogger<BookingNotificationStep> _logger;

    public BookingNotificationStep(
        IBookingNotificationPublisher publisher,
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
        IReadOnlyList<BookingNotificationRecipient> recipients,
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
            return (
                LifecycleStepStatuses.Skipped,
                BookingWorkflowNotificationOutcomeStatuses.SkippedNoMapping,
                "Lifecycle event has no notification mapping.");
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

            _logger.LogInformation(
                "Booking notification policy recipient types requested. NotificationType={NotificationType} RecipientTypes={RecipientTypes}",
                notificationType.Name,
                string.Join(',', policy.Recipients
                    .Where(x => x.Enabled)
                    .Select(x => x.RecipientType)
                    .Distinct(StringComparer.OrdinalIgnoreCase)));

            if (!policy.Enabled)
            {
                _logger.LogInformation(
                    "Booking notification skipped because policy is disabled. NotificationType={NotificationType}",
                    notificationType.Name);
                return (
                    LifecycleStepStatuses.Skipped,
                    BookingWorkflowNotificationOutcomeStatuses.SkippedPolicyDisabled,
                    "Notification policy is disabled.");
            }

            if (policy.Channels.Count(x => x.Enabled) == 0)
            {
                _logger.LogInformation(
                    "Booking notification skipped because policy has no enabled channels. NotificationType={NotificationType}",
                    notificationType.Name);
                return (
                    LifecycleStepStatuses.Skipped,
                    BookingWorkflowNotificationOutcomeStatuses.SkippedNoChannels,
                    "No notification channels are enabled.");
            }

            var resolvedRecipients = await _recipientResolver.ResolveAsync(policy, recipients, data, ct);
            _logger.LogInformation(
                "Booking notification recipients resolved. NotificationType={NotificationType} RecipientCount={RecipientCount} RecipientCountsByType={RecipientCountsByType}",
                notificationType.Name,
                resolvedRecipients.Count,
                string.Join(',', resolvedRecipients
                    .GroupBy(x => x.RecipientType, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"{x.Key}:{x.Count()}")));

            if (resolvedRecipients.Count == 0)
            {
                _logger.LogInformation(
                    "Booking notification skipped because no recipients resolved. NotificationType={NotificationType}",
                    notificationType.Name);
                return (
                    LifecycleStepStatuses.Skipped,
                    BookingWorkflowNotificationOutcomeStatuses.SkippedNoRecipients,
                    "No notification recipients resolved.");
            }

            _logger.LogInformation(
                "Booking notification publish started. NotificationType={NotificationType} PublisherTransport={PublisherTransport}",
                notificationType.Name,
                _publisher.GetType().Name);

            foreach (var recipientGroup in resolvedRecipients.GroupBy(x => x.RecipientType, StringComparer.OrdinalIgnoreCase))
            {
                var recipientType = recipientGroup.Key;
                await _publisher.PublishAsync(
                    new BookingNotificationRequest(
                        notificationType,
                        correlationId,
                        new BookingNotificationActor(actorType, "Booking", null, null, null),
                        recipientGroup.ToArray(),
                        BuildPolicyData(data, policy, notificationType, recipientType)),
                    ct);
            }

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

    private static BookingNotificationType? MapEventType(string lifecycleEventType) => lifecycleEventType switch
    {
        LifecycleEventTypes.Booked => BookingNotificationTypes.BookingConfirmed,
        LifecycleEventTypes.Cancelled => BookingNotificationTypes.BookingCancelled,
        LifecycleEventTypes.Rearranged => BookingNotificationTypes.BookingRescheduled,
        LifecycleEventTypes.HoldCreated => BookingNotificationTypes.BookingHoldCreated,
        _ => BookingNotificationTypes.TryGetByName(lifecycleEventType)
    };

    private static IReadOnlyDictionary<string, string> BuildPolicyData(
        IReadOnlyDictionary<string, string> data,
        BookingNotificationPolicy policy,
        BookingNotificationType notificationType,
        string recipientType)
    {
        var includeClientLinks = IsClientRecipient(recipientType);
        var enriched = data
            .Where(kvp => includeClientLinks || !IsClientOnlyDataKey(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        enriched["RecipientType"] = recipientType;
        enriched["recipientType"] = recipientType;

        foreach (var channel in policy.Channels.Where(x => x.Enabled))
        {
            var recipientTemplateKey = ResolveRecipientTemplateKey(channel.TemplateKey, notificationType, recipientType);
            enriched[$"TemplateKey:{channel.Channel}"] = ResolveModeTemplateKey(recipientTemplateKey, data);
            enriched[$"TemplateVersion:{channel.Channel}"] = channel.TemplateVersion;
        }

        return enriched;
    }

    private static string ResolveRecipientTemplateKey(
        string templateKey,
        BookingNotificationType notificationType,
        string recipientType)
    {
        if (IsClientRecipient(recipientType))
            return templateKey;

        var suffix = NormalizeRecipientType(recipientType);
        return string.IsNullOrWhiteSpace(suffix)
            ? templateKey
            : $"{templateKey}-{suffix}";
    }

    private static string ResolveModeTemplateKey(
        string templateKey,
        IReadOnlyDictionary<string, string> data)
    {
        if (!data.TryGetValue("meetingMode", out var meetingMode) || string.IsNullOrWhiteSpace(meetingMode))
            return templateKey;

        var suffix = NormalizeMeetingMode(meetingMode);
        return string.IsNullOrWhiteSpace(suffix) ? templateKey : $"{templateKey}-{suffix}";
    }

    private static bool IsClientRecipient(string recipientType)
        => recipientType.Equals(BookingNotificationRecipientTypes.Client, StringComparison.OrdinalIgnoreCase);

    private static bool IsClientOnlyDataKey(string key)
        => key.Contains("token", StringComparison.OrdinalIgnoreCase)
           || ClientOnlyDataKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeRecipientType(string recipientType)
    {
        var normalized = new string(recipientType
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized switch
        {
            "contactcentre" => "contact-centre",
            "operationsmanager" => "operations-manager",
            "reportingmanager" => "reporting-manager",
            "orgadmin" => "admin",
            _ => normalized
        };
    }

    private static string NormalizeMeetingMode(string meetingMode)
    {
        var normalized = new string(meetingMode
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized switch
        {
            "online" or "remote" => "online",
            "facetoface" or "inperson" or "inpersonmeeting" => "face-to-face",
            _ => string.Empty
        };
    }
}
