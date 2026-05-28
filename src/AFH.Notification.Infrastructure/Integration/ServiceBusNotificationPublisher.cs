using System.Text.Json;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Integration;

public sealed class ServiceBusNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusNotificationPublisher(IOptions<ServiceBusNotificationPublisherOptions> options)
    {
        var opts = options.Value;
        var entityName = !string.IsNullOrWhiteSpace(opts.TopicName)
            ? opts.TopicName
            : opts.QueueName;

        if (string.IsNullOrWhiteSpace(entityName))
            throw new InvalidOperationException($"{ServiceBusNotificationPublisherOptions.SectionName}:TopicName or QueueName is required.");

        _client = !string.IsNullOrWhiteSpace(opts.ConnectionString)
            ? new ServiceBusClient(opts.ConnectionString)
            : !string.IsNullOrWhiteSpace(opts.FullyQualifiedNamespace)
                ? new ServiceBusClient(opts.FullyQualifiedNamespace, new DefaultAzureCredential())
                : throw new InvalidOperationException($"{ServiceBusNotificationPublisherOptions.SectionName}:ConnectionString or FullyQualifiedNamespace is required.");

        _sender = _client.CreateSender(entityName);
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        await _sender.SendMessageAsync(CreateServiceBusMessage(notification), ct);
    }

    public static ServiceBusMessage CreateServiceBusMessage(NotificationRequested notification)
        => new(BinaryData.FromString(JsonSerializer.Serialize(notification, SerializerOptions)))
        {
            ContentType = "application/json",
            CorrelationId = notification.CorrelationId,
            MessageId = BuildMessageId(notification)
        };

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }

    private static string BuildMessageId(NotificationRequested notification)
        => notification.Data.TryGetValue("IdempotencyKey", out var idempotencyKey) && !string.IsNullOrWhiteSpace(idempotencyKey)
            ? idempotencyKey
            : $"{notification.SourceSystem}:{notification.Type.Name}:{notification.CorrelationId}";
}
