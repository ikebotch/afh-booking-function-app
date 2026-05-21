using AFH.Booking.Application.Models.Clients;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IEmailNotificationSender
{
    Task<EmailNotificationSendResult> SendAsync(EmailNotificationMessage message, CancellationToken ct);
}
