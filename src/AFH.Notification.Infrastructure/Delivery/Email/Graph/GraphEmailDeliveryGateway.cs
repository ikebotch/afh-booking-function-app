using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace AFH.Notification.Infrastructure.Delivery.Email.Graph;

public sealed class GraphEmailDeliveryGateway : INotificationDeliveryGateway
{
    private readonly GraphEmailOptions _options;
    private readonly IGraphEmailSender _sender;
    private readonly ILogger<GraphEmailDeliveryGateway> _logger;

    public GraphEmailDeliveryGateway(
        IOptions<GraphEmailOptions> options,
        IGraphEmailSender sender,
        ILogger<GraphEmailDeliveryGateway> logger)
    {
        _options = options.Value;
        _options.Validate();
        _sender = sender;
        _logger = logger;
    }

    public bool CanSend(NotificationChannel channel)
        => channel == NotificationChannel.Email;

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient.Email))
            return new NotificationDeliveryResult("Skipped", null, "Graph");

        var providerCorrelationId = $"graph-sendmail-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Sending queued notification email through Microsoft Graph. Recipient={Recipient} SenderMailbox={SenderMailbox} CorrelationId={CorrelationId} ProviderCorrelationId={ProviderCorrelationId} Subject={Subject} TextLength={TextLength}",
            request.Recipient.Email,
            _options.SenderMailbox,
            request.CorrelationId,
            providerCorrelationId,
            request.Subject,
            request.TextBody.Length);

        await _sender.SendAsync(_options.SenderMailbox!, request, providerCorrelationId, ct);

        // Microsoft Graph sendMail returns 202 Accepted with no provider message id in the response.
        // Store an internal correlation id so dispatch/audit records can still be traced.
        return new NotificationDeliveryResult("GraphAccepted", providerCorrelationId, "Graph");
    }
}

public interface IGraphEmailSender
{
    Task SendAsync(
        string senderMailbox,
        NotificationDeliveryRequest request,
        string providerCorrelationId,
        CancellationToken ct);
}

public sealed class GraphEmailSender : IGraphEmailSender
{
    private readonly GraphServiceClient _graphClient;

    public GraphEmailSender(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
    }

    public Task SendAsync(
        string senderMailbox,
        NotificationDeliveryRequest request,
        string providerCorrelationId,
        CancellationToken ct)
    {
        var message = new Message
        {
            Subject = request.Subject ?? string.Empty,
            Body = new ItemBody
            {
                ContentType = string.IsNullOrWhiteSpace(request.HtmlBody) ? BodyType.Text : BodyType.Html,
                Content = string.IsNullOrWhiteSpace(request.HtmlBody) ? request.TextBody : request.HtmlBody
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = request.Recipient.Email
                    }
                }
            ],
            InternetMessageHeaders =
            [
                new InternetMessageHeader
                {
                    Name = "x-afh-notification-correlation-id",
                    Value = request.CorrelationId
                },
                new InternetMessageHeader
                {
                    Name = "x-afh-provider-correlation-id",
                    Value = providerCorrelationId
                }
            ]
        };

        var attachments = BuildAttachments(request);
        if (attachments is not null)
            message.Attachments = attachments;

        var body = new SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = true
        };

        return _graphClient.Users[senderMailbox].SendMail.PostAsync(body, cancellationToken: ct);
    }

    private static List<Attachment>? BuildAttachments(NotificationDeliveryRequest request)
    {
        if (request.Attachments is null || request.Attachments.Count == 0)
            return null;

        var attachments = new List<Attachment>();
        foreach (var attachment in request.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.FileName) || string.IsNullOrWhiteSpace(attachment.Base64Content))
                continue;

            attachments.Add(new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = attachment.FileName,
                ContentType = attachment.ContentType,
                ContentBytes = Convert.FromBase64String(attachment.Base64Content),
                ContentId = attachment.ContentId,
                IsInline = attachment.Inline
            });
        }

        return attachments.Count == 0 ? null : attachments;
    }
}

public static class GraphEmailClientFactory
{
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];

    public static GraphServiceClient Create(GraphEmailOptions options)
    {
        options.Validate();

        TokenCredential credential = options.UseManagedIdentity
            ? CreateManagedIdentityCredential(options)
            : new ClientSecretCredential(options.TenantId!, options.ClientId!, options.ClientSecret!);

        return new GraphServiceClient(credential, Scopes);
    }

    private static TokenCredential CreateManagedIdentityCredential(GraphEmailOptions options)
        => string.IsNullOrWhiteSpace(options.ClientId)
            ? new ManagedIdentityCredential()
            : new ManagedIdentityCredential(options.ClientId);
}
