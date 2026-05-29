using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Infrastructure.Options;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Notifications;

public sealed class BookingNotificationRecipientResolver : IBookingNotificationRecipientResolver
{
    private readonly IAdviserProfileProjectionRepository _advisers;
    private readonly NotificationEmailOptions _emailOptions;
    private readonly ILogger<BookingNotificationRecipientResolver> _logger;

    public BookingNotificationRecipientResolver(
        IAdviserProfileProjectionRepository advisers,
        IOptions<NotificationEmailOptions> emailOptions,
        ILogger<BookingNotificationRecipientResolver> logger)
    {
        _advisers = advisers;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        BookingNotificationPolicy policy,
        IReadOnlyList<NotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        var enabledChannels = policy.Channels
            .Where(x => x.Enabled)
            .Select(x => x.Channel)
            .Where(x => x != NotificationChannel.Unknown)
            .Distinct()
            .ToArray();

        if (enabledChannels.Length == 0)
            return Array.Empty<NotificationRecipient>();

        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<NotificationRecipient>();

        foreach (var recipientPolicy in policy.Recipients.Where(x => x.Enabled))
        {
            var candidates = await ResolveRecipientsAsync(recipientPolicy.RecipientType, requestedRecipients, data, ct);
            if (candidates.Count == 0)
            {
                _logger.LogWarning(
                    "Notification recipient skipped because recipient type could not be resolved. NotificationType={NotificationType} RecipientType={RecipientType}",
                    policy.NotificationType,
                    recipientPolicy.RecipientType);
                continue;
            }

            foreach (var candidate in candidates)
            {
                var channels = enabledChannels
                    .Where(channel => HasTarget(candidate, channel))
                    .Where(channel => usedTargets.Add(GetTargetKey(candidate, channel)))
                    .ToArray();

                if (channels.Length == 0)
                {
                    _logger.LogWarning(
                        "Notification recipient skipped because no enabled channel target is available or target was already used. NotificationType={NotificationType} RecipientType={RecipientType}",
                        policy.NotificationType,
                        recipientPolicy.RecipientType);
                    continue;
                }

                resolved.Add(candidate with { PreferredChannels = channels });
            }
        }

        return resolved;
    }

    private async Task<IReadOnlyList<NotificationRecipient>> ResolveRecipientsAsync(
        string recipientType,
        IReadOnlyList<NotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        var requested = requestedRecipients.FirstOrDefault(x =>
            string.Equals(x.RecipientType, recipientType, StringComparison.OrdinalIgnoreCase));

        if (requested is not null)
            return [requested];

        if (string.Equals(recipientType, BookingNotificationRecipientTypes.Adviser, StringComparison.OrdinalIgnoreCase))
        {
            var adviser = await ResolveAdviserAsync(data, ct);
            return adviser is null ? [] : [adviser];
        }

        if (string.Equals(recipientType, BookingNotificationRecipientTypes.ContactCentre, StringComparison.OrdinalIgnoreCase))
            return ResolveContactCentre();

        return [];
    }

    private async Task<NotificationRecipient?> ResolveAdviserAsync(
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        if (!data.TryGetValue("adviserId", out var adviserId) || string.IsNullOrWhiteSpace(adviserId))
            return null;

        var adviser = await _advisers.GetAsync(adviserId.Trim(), ct);
        var email = adviser?.MailboxUserId;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            return null;

        return new NotificationRecipient(
            BookingNotificationRecipientTypes.Adviser,
            string.IsNullOrWhiteSpace(adviser?.DisplayName)
                ? (data.TryGetValue("adviserName", out var adviserName) ? adviserName : null)
                : adviser.DisplayName,
            email.Trim(),
            null);
    }

    private IReadOnlyList<NotificationRecipient> ResolveContactCentre()
    {
        var configured = !string.IsNullOrWhiteSpace(_emailOptions.AdminBccRecipients)
            ? _emailOptions.AdminBccRecipients
            : _emailOptions.ContactCentreEmailAddress;

        return SplitEmailAddresses(configured)
            .Select(email => new NotificationRecipient(
                BookingNotificationRecipientTypes.ContactCentre,
                "Contact Centre",
                email,
                null))
            .ToArray();
    }

    private static string[] SplitEmailAddresses(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static bool HasTarget(NotificationRecipient recipient, NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => !string.IsNullOrWhiteSpace(recipient.Email),
            NotificationChannel.Sms => !string.IsNullOrWhiteSpace(recipient.MobileNumber),
            NotificationChannel.Push => !string.IsNullOrWhiteSpace(recipient.PushTarget),
            _ => false
        };

    private static string GetTargetKey(NotificationRecipient recipient, NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => $"{channel}:{recipient.Email?.Trim()}",
            NotificationChannel.Sms => $"{channel}:{recipient.MobileNumber?.Trim()}",
            NotificationChannel.Push => $"{channel}:{recipient.PushTarget?.Trim()}",
            _ => $"{channel}:"
        };
}