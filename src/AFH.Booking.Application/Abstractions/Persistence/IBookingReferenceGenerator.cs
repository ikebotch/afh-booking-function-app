namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IBookingReferenceGenerator
{
    Task<string> GenerateBookingReferenceAsync(string bookingId, CancellationToken ct);

    Task<string> GenerateApprovalRequestReferenceAsync(string approvalRequestId, CancellationToken ct);
}
