using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Requests;
using AFH.Notification.Infrastructure.Integration.Inbound;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace AFH.Booking.Function.Functions.V1.Notifications.Inbound;

public sealed class ReceiveNotificationRequestServiceBusFunction
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly INotificationRequestIngestionService _ingestionService;
    private readonly NotificationInboundServiceBusOptions _options;
    private readonly ILogger<ReceiveNotificationRequestServiceBusFunction> _logger;

    public ReceiveNotificationRequestServiceBusFunction(
        INotificationRequestIngestionService ingestionService,
        IOptions<NotificationInboundServiceBusOptions> options,
        ILogger<ReceiveNotificationRequestServiceBusFunction> logger)
    {
        _ingestionService = ingestionService;
        _options = options.Value;
        _logger = logger;
    }

    //[Function("Notifications_RequestServiceBusTopicV1")]
    //[Disable("Notifications:Inbound:ServiceBus:Disabled")]
    //public async Task RunTopicAsync(
    //    [ServiceBusTrigger("%Notifications:Inbound:ServiceBus:TopicName%", "%Notifications:Inbound:ServiceBus:SubscriptionName%", Connection = "Notifications:Inbound:ServiceBus:ConnectionString", AutoCompleteMessages = false)]
    //    ServiceBusReceivedMessage message,
    //    ServiceBusMessageActions messageActions,
    //    FunctionContext context)
    //{
    //    await RunCoreAsync(message, messageActions, context.CancellationToken);
    //}

    public async Task RunCoreAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            await messageActions.CompleteMessageAsync(message, ct);
            return;
        }

        NotificationRequested? request;
        try
        {
            request = JsonSerializer.Deserialize<NotificationRequested>(message.Body, SerializerOptions);
        }
        catch (JsonException ex)
        {
            await DeadLetterAsync(message, messageActions, "InvalidJson", "Message body must be valid NotificationRequested JSON.", ct);
            _logger.LogWarning(ex, "Dead-lettered invalid notification request JSON. MessageId={MessageId}", message.MessageId);
            return;
        }

        if (request is null)
        {
            await DeadLetterAsync(message, messageActions, "InvalidRequest", "Message body is required.", ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.CorrelationId) && !string.IsNullOrWhiteSpace(message.CorrelationId))
            request = request with { CorrelationId = message.CorrelationId };

        try
        {
            await _ingestionService.AcceptAsync(request, ct);
            await messageActions.CompleteMessageAsync(message, ct);
        }
        catch (NotificationRequestValidationException ex)
        {
            await DeadLetterAsync(message, messageActions, "Validation", ex.Message, ct);
        }
    }

    private static Task DeadLetterAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        string reason,
        string description,
        CancellationToken ct)
        => messageActions.DeadLetterMessageAsync(
            message,
            propertiesToModify: null,
            deadLetterReason: reason,
            deadLetterErrorDescription: description,
            cancellationToken: ct);
}
