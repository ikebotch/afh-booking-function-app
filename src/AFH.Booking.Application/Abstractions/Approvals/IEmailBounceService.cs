using AFH.Booking.Application.Models.Approvals;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IEmailBounceService
{
    Task<EmailBounceEventResponse> RecordBounceAsync(EmailBounceWebhookRequest request, CancellationToken ct);
}
