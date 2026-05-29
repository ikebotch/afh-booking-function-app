using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.BusinessContacts;
using AFH.Booking.Application.Models.Notifications;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Notifications;

public sealed class BookingNotificationRecipientResolver : IBookingNotificationRecipientResolver
{
    private readonly IAdviserProfileProjectionRepository _advisers;
    private readonly IBookingBusinessContactsClient _businessContacts;
    private readonly ILogger<BookingNotificationRecipientResolver> _logger;

    public BookingNotificationRecipientResolver(
        IAdviserProfileProjectionRepository advisers,
        IBookingBusinessContactsClient businessContacts,
        ILogger<BookingNotificationRecipientResolver> logger)
    {
        _advisers = advisers;
        _businessContacts = businessContacts;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookingNotificationRecipient>> ResolveAsync(
        BookingNotificationPolicy policy,
        IReadOnlyList<BookingNotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        var enabledChannels = policy.Channels
            .Where(x => x.Enabled)
            .Select(x => x.Channel)
            .Where(x => x != BookingNotificationChannel.Unknown)
            .Distinct()
            .ToArray();

        if (enabledChannels.Length == 0)
            return Array.Empty<BookingNotificationRecipient>();

        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<BookingNotificationRecipient>();
        var businessContactTypes = policy.Recipients
            .Where(x => x.Enabled)
            .Select(x => x.RecipientType)
            .Where(IsBusinessContactType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var businessContacts = businessContactTypes.Length == 0
            ? []
            : await ResolveBusinessContactsAsync(businessContactTypes, data, policy.NotificationType, ct);

        foreach (var recipientPolicy in policy.Recipients.Where(x => x.Enabled))
        {
            var candidates = await ResolveRecipientsAsync(recipientPolicy.RecipientType, requestedRecipients, data, businessContacts, ct);
            if (candidates.Count == 0)
            {
                _logger.LogWarning(
                    "Notification recipient skipped because recipient type could not be resolved. NotificationType={NotificationType} BookingId={BookingId} CorrelationId={CorrelationId} RecipientType={RecipientType}",
                    policy.NotificationType,
                    GetDataValue(data, "bookingId"),
                    GetDataValue(data, "correlationId") ?? GetDataValue(data, "bookingId"),
                    recipientPolicy.RecipientType);
                continue;
            }

            foreach (var candidate in candidates)
            {
                var channels = enabledChannels
                    .Where(channel => IsAllowedByCandidate(candidate, channel))
                    .Where(channel => HasTarget(candidate, channel))
                    .Where(channel => usedTargets.Add(GetTargetKey(candidate, channel)))
                    .ToArray();

                if (channels.Length == 0)
                {
                    _logger.LogWarning(
                        "Notification recipient skipped because no enabled channel target is available or target was already used. NotificationType={NotificationType} BookingId={BookingId} CorrelationId={CorrelationId} RecipientType={RecipientType}",
                        policy.NotificationType,
                        GetDataValue(data, "bookingId"),
                        GetDataValue(data, "correlationId") ?? GetDataValue(data, "bookingId"),
                        recipientPolicy.RecipientType);
                    continue;
                }

                resolved.Add(candidate with { PreferredChannels = channels });
            }
        }

        return resolved;
    }

    private async Task<IReadOnlyList<BookingNotificationRecipient>> ResolveRecipientsAsync(
        string recipientType,
        IReadOnlyList<BookingNotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        IReadOnlyList<BookingBusinessContact> businessContacts,
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

        if (IsBusinessContactType(recipientType))
            return businessContacts
                .Where(x => string.Equals(x.ContactType, recipientType, StringComparison.OrdinalIgnoreCase))
                .Select(ToRecipient)
                .ToArray();

        return [];
    }

    private async Task<IReadOnlyList<BookingBusinessContact>> ResolveBusinessContactsAsync(
        IReadOnlyList<string> contactTypes,
        IReadOnlyDictionary<string, string> data,
        string notificationType,
        CancellationToken ct)
    {
        var contacts = await _businessContacts.GetContactsAsync(
            new BookingBusinessContactSearch(
                contactTypes,
                GetDataValue(data, "adviserId"),
                GetDataValue(data, "region"),
                GetDataValue(data, "organisationId") ?? GetDataValue(data, "organizationId"),
                GetDataValue(data, "clientId")),
            ct);

        var missing = contactTypes
            .Where(contactType => !contacts.Any(contact => string.Equals(contact.ContactType, contactType, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (missing.Length > 0)
        {
            _logger.LogWarning(
                "Business contact roles could not be resolved for booking notification. NotificationType={NotificationType} BookingId={BookingId} CorrelationId={CorrelationId} MissingContactRoles={MissingContactRoles}",
                notificationType,
                GetDataValue(data, "bookingId"),
                GetDataValue(data, "correlationId") ?? GetDataValue(data, "bookingId"),
                string.Join(',', missing));
        }

        return contacts;
    }

    private async Task<BookingNotificationRecipient?> ResolveAdviserAsync(
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        if (!data.TryGetValue("adviserId", out var adviserId) || string.IsNullOrWhiteSpace(adviserId))
            return null;

        var adviser = await _advisers.GetAsync(adviserId.Trim(), ct);
        var email = adviser?.MailboxUserId;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            return null;

        return new BookingNotificationRecipient(
            BookingNotificationRecipientTypes.Adviser,
            string.IsNullOrWhiteSpace(adviser?.DisplayName)
                ? (data.TryGetValue("adviserName", out var adviserName) ? adviserName : null)
                : adviser.DisplayName,
            email.Trim(),
            null);
    }

    private static BookingNotificationRecipient ToRecipient(BookingBusinessContact contact)
        => new(
            contact.ContactType,
            contact.DisplayName,
            contact.Email,
            contact.MobileNumber,
            PreferredChannels: contact.Channels);

    private static bool IsBusinessContactType(string recipientType)
        => string.Equals(recipientType, BookingNotificationRecipientTypes.ContactCentre, StringComparison.OrdinalIgnoreCase)
           || string.Equals(recipientType, BookingNotificationRecipientTypes.OrgAdmin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(recipientType, BookingNotificationRecipientTypes.Manager, StringComparison.OrdinalIgnoreCase)
           || string.Equals(recipientType, BookingNotificationRecipientTypes.Fallback, StringComparison.OrdinalIgnoreCase);

    private static string? GetDataValue(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static bool IsAllowedByCandidate(BookingNotificationRecipient recipient, BookingNotificationChannel channel)
        => recipient.PreferredChannels is null
           || recipient.PreferredChannels.Count == 0
           || recipient.PreferredChannels.Contains(channel);

    private static bool HasTarget(BookingNotificationRecipient recipient, BookingNotificationChannel channel)
        => channel switch
        {
            BookingNotificationChannel.Email => !string.IsNullOrWhiteSpace(recipient.Email),
            BookingNotificationChannel.Sms => !string.IsNullOrWhiteSpace(recipient.MobileNumber),
            BookingNotificationChannel.Push => !string.IsNullOrWhiteSpace(recipient.PushTarget),
            _ => false
        };

    private static string GetTargetKey(BookingNotificationRecipient recipient, BookingNotificationChannel channel)
        => channel switch
        {
            BookingNotificationChannel.Email => $"{channel}:{recipient.Email?.Trim()}",
            BookingNotificationChannel.Sms => $"{channel}:{recipient.MobileNumber?.Trim()}",
            BookingNotificationChannel.Push => $"{channel}:{recipient.PushTarget?.Trim()}",
            _ => $"{channel}:"
        };
}
