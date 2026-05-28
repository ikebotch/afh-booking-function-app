using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed class NotificationRequestIngestionService : INotificationRequestIngestionService
{
    private readonly NotificationOutboxService _outboxService;

    public NotificationRequestIngestionService(NotificationOutboxService outboxService)
    {
        _outboxService = outboxService;
    }

    public async Task<NotificationRequestAcceptedResult> AcceptAsync(NotificationRequested request, CancellationToken ct)
    {
        var normalized = Normalize(request);
        Validate(normalized);

        var result = await _outboxService.AcceptAsync(normalized, ct);
        if (result.Items.Count == 0)
            throw new NotificationRequestValidationException("Notification request did not resolve to any recipient/channel delivery attempts.");

        return new NotificationRequestAcceptedResult(
            result.RequestId,
            "Accepted",
            normalized.CorrelationId,
            result.CreatedAny);
    }

    private static NotificationRequested Normalize(NotificationRequested request)
    {
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId.Trim();

        return request with
        {
            CorrelationId = correlationId,
            Type = new NotificationType(
                request.Type.SourceApplication?.Trim() ?? string.Empty,
                request.Type.Name?.Trim() ?? string.Empty),
            Recipients = request.Recipients ?? [],
            Data = request.Data ?? new Dictionary<string, string>()
        };
    }

    private static void Validate(NotificationRequested request)
    {
        if (string.IsNullOrWhiteSpace(request.Type.SourceApplication))
            throw new NotificationRequestValidationException("SourceApplication is required.");

        if (string.IsNullOrWhiteSpace(request.Type.Name))
            throw new NotificationRequestValidationException("NotificationType is required.");

        if (string.IsNullOrWhiteSpace(request.CorrelationId))
            throw new NotificationRequestValidationException("CorrelationId is required.");

        if (request.Recipients.Count == 0)
            throw new NotificationRequestValidationException("At least one recipient is required.");

        var combinations = request.Recipients
            .SelectMany(GetRequestedChannels, (recipient, channel) => new { Recipient = recipient, Channel = channel })
            .Where(x => HasTarget(x.Recipient, x.Channel))
            .ToArray();

        if (combinations.Length == 0)
            throw new NotificationRequestValidationException("At least one recipient/channel combination with a delivery target is required.");

        foreach (var channel in combinations.Select(x => x.Channel).Distinct())
        {
            if (!HasTemplateForChannel(request, channel))
                throw new NotificationRequestValidationException($"TemplateKey and TemplateVersion are required for channel {channel}.");
        }
    }

    private static IEnumerable<NotificationChannel> GetRequestedChannels(NotificationRecipient recipient)
    {
        if (recipient.PreferredChannels is { Count: > 0 })
            return recipient.PreferredChannels.Where(channel => channel != NotificationChannel.Unknown).Distinct();

        var channels = new List<NotificationChannel>();
        if (!string.IsNullOrWhiteSpace(recipient.Email))
            channels.Add(NotificationChannel.Email);
        if (!string.IsNullOrWhiteSpace(recipient.MobileNumber))
            channels.Add(NotificationChannel.Sms);
        if (!string.IsNullOrWhiteSpace(recipient.PushTarget))
            channels.Add(NotificationChannel.Push);

        return channels;
    }

    private static bool HasTarget(NotificationRecipient recipient, NotificationChannel channel)
        => channel switch
        {
            NotificationChannel.Email => !string.IsNullOrWhiteSpace(recipient.Email),
            NotificationChannel.Sms => !string.IsNullOrWhiteSpace(recipient.MobileNumber),
            NotificationChannel.Push => !string.IsNullOrWhiteSpace(recipient.PushTarget),
            _ => false
        };

    private static bool HasTemplateForChannel(NotificationRequested request, NotificationChannel channel)
        => HasValue(request.Data, "TemplateKey") && HasValue(request.Data, "TemplateVersion") ||
           HasValue(request.Data, $"TemplateKey:{channel}") && HasValue(request.Data, $"TemplateVersion:{channel}");

    private static bool HasValue(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
}
