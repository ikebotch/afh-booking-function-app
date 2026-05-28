using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Application.Services.Notifications;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Notification.Contract.V1.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Notifications;

public sealed class BookingNotificationPolicyProvider : IBookingNotificationPolicyProvider
{
    private readonly BookingDbContext _db;

    public BookingNotificationPolicyProvider(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<BookingNotificationPolicy> GetAsync(
        string sourceApplication,
        NotificationType notificationType,
        CancellationToken ct)
    {
        var source = string.IsNullOrWhiteSpace(sourceApplication)
            ? BookingNotificationDefaults.SourceApplication
            : sourceApplication.Trim();

        var defaults = BookingNotificationDefaults.CreatePolicy(source, notificationType);
        var row = await _db.BookingNotificationRules
            .AsNoTracking()
            .Include(x => x.Channels)
            .Include(x => x.Recipients)
            .SingleOrDefaultAsync(
                x => x.SourceApplication == source && x.NotificationType == notificationType.Name,
                ct);

        if (row is null)
            return defaults;

        return new BookingNotificationPolicy(
            row.SourceApplication,
            row.NotificationType,
            row.Enabled,
            MergeChannels(defaults, row.Channels),
            MergeRecipients(defaults, row.Recipients));
    }

    private static IReadOnlyList<BookingNotificationChannelPolicy> MergeChannels(
        BookingNotificationPolicy defaults,
        IReadOnlyCollection<Persistence.Models.BookingNotificationRuleChannelModel> rows)
    {
        var channels = rows
            .Select(row => Enum.TryParse<NotificationChannel>(row.Channel, ignoreCase: true, out var channel)
                ? new BookingNotificationChannelPolicy(channel, row.Enabled, row.TemplateKey, row.TemplateVersion)
                : null)
            .Where(x => x is not null)
            .Cast<BookingNotificationChannelPolicy>()
            .ToDictionary(x => x.Channel);

        foreach (var defaultChannel in defaults.Channels)
            channels.TryAdd(defaultChannel.Channel, defaultChannel);

        return channels.Values.OrderBy(x => x.Channel.ToString(), StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<BookingNotificationRecipientPolicy> MergeRecipients(
        BookingNotificationPolicy defaults,
        IReadOnlyCollection<Persistence.Models.BookingNotificationRuleRecipientModel> rows)
    {
        var recipients = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.RecipientType))
            .Select(row => new BookingNotificationRecipientPolicy(row.RecipientType.Trim(), row.Enabled))
            .ToDictionary(x => x.RecipientType, StringComparer.OrdinalIgnoreCase);

        foreach (var defaultRecipient in defaults.Recipients)
            recipients.TryAdd(defaultRecipient.RecipientType, defaultRecipient);

        return recipients.Values.OrderBy(x => x.RecipientType, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
