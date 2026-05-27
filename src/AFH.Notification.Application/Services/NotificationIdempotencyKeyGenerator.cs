using System.Security.Cryptography;
using System.Text;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed class NotificationIdempotencyKeyGenerator : INotificationIdempotencyKeyGenerator
{
    private readonly IReadOnlyList<INotificationIdempotencyPolicy> _policies;

    public NotificationIdempotencyKeyGenerator(IEnumerable<INotificationIdempotencyPolicy> policies)
    {
        _policies = policies.ToArray();
    }

    public string GenerateKey(NotificationRequested request, NotificationChannel channel, NotificationRecipient recipient)
    {
        var primaryId = GetPrimaryId(request).Trim().ToLowerInvariant();
        var templateVersion = (request.Data.TryGetValue("TemplateVersion", out var version) ? version : "v1").Trim().ToLowerInvariant();
        
        var recipientAddress = channel switch
        {
            NotificationChannel.Email => recipient.Email,
            NotificationChannel.Sms => recipient.MobileNumber,
            NotificationChannel.Push => recipient.PushTarget,
            _ => throw new InvalidOperationException($"Unsupported channel for idempotency key: {channel}")
        };

        if (string.IsNullOrWhiteSpace(recipientAddress))
        {
            throw new InvalidOperationException($"Recipient target is missing for channel {channel}");
        }

        recipientAddress = recipientAddress.Trim().ToLowerInvariant();
        var sourceSystem = request.SourceSystem.Trim().ToLowerInvariant();
        var typeName = request.Type.Name.Trim().ToLowerInvariant();
        var channelName = channel.ToString().Trim().ToLowerInvariant();
        var recipientType = recipient.RecipientType.Trim().ToLowerInvariant();

        var rawKey = $"{sourceSystem}:{typeName}:{primaryId}:{channelName}:{recipientType}:{recipientAddress}:{templateVersion}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"{sourceSystem}:{typeName}:{hashString}";
    }

    private string GetPrimaryId(NotificationRequested request)
        => _policies.FirstOrDefault(policy => policy.CanHandle(request))?.GetPrimaryId(request)
           ?? request.CorrelationId;
}
