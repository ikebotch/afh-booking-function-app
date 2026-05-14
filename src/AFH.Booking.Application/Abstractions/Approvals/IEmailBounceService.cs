using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Approvals;

public interface IEmailBounceService
{
    Task<EmailBounceEventResponse> RecordBounceAsync(EmailBounceWebhookRequest request, CancellationToken ct);
}
