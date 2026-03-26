namespace AFH.Booking.Application.Abstractions.Clients;

public interface IEmailNotificationSender
{
    Task<EmailNotificationSendResult> SendAsync(EmailNotificationMessage message, CancellationToken ct);
}

public sealed record EmailNotificationMessage(
    string RecipientEmail,
    string Subject,
    string HtmlBody,
    string TextBody);

public sealed record EmailNotificationSendResult(
    string Status,
    string? ProviderMessageId = null);
