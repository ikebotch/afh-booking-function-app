using System.Text.Json;
using Azure.Storage.Queues;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Queue;

public sealed class AzureStorageNotificationQueuePublisher : INotificationQueuePublisher
{
    private readonly QueueClient _queueClient;

    public AzureStorageNotificationQueuePublisher(IOptions<NotificationQueueOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new ArgumentException("Queue connection string is not configured.", nameof(options));
        }

        _queueClient = new QueueClient(opts.ConnectionString, opts.QueueName, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        });
    }

    public async Task PublishAsync(NotificationQueueMessage message, CancellationToken ct)
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var payload = JsonSerializer.Serialize(message);
        await _queueClient.SendMessageAsync(payload, cancellationToken: ct);
    }
}
