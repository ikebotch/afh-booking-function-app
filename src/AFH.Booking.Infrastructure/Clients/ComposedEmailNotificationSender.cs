using AFH.Booking.Application.Abstractions.Clients;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class ComposedEmailNotificationSender : IEmailNotificationSender
{
    private readonly ILogger<ComposedEmailNotificationSender> _logger;

    public ComposedEmailNotificationSender(ILogger<ComposedEmailNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task<EmailNotificationSendResult> SendAsync(EmailNotificationMessage message, CancellationToken ct)
    {
        _logger.LogInformation(
            "Composed booking email for {Recipient}. Subject={Subject} HtmlLength={HtmlLength} TextLength={TextLength}",
            message.RecipientEmail,
            message.Subject,
            message.HtmlBody.Length,
            message.TextBody.Length);

        return Task.FromResult(new EmailNotificationSendResult(
            Status: "Composed",
            ProviderMessageId: Guid.NewGuid().ToString("N")[..20]));
    }
}
