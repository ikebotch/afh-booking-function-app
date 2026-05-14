using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Persistence;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class ApprovalNotificationService : IApprovalNotificationService
{
    private readonly INotificationDispatchRepository _dispatches;
    private readonly IUnitOfWork _uow;

    public ApprovalNotificationService(
        INotificationDispatchRepository dispatches,
        IUnitOfWork uow)
    {
        _dispatches = dispatches;
        _uow = uow;
    }

    public async Task RecordRequestSubmittedAsync(
        ApprovalRouteTarget routeTarget,
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string changeType,
        string reasonCode,
        string? reasonDetail,
        CancellationToken ct)
    {
        await AddAsync(
            bookingId,
            transactionId,
            transactionRef,
            "ApprovalSubmitted",
            routeTarget.TargetValue,
            $"Adviser request submitted for {changeType}. Reason: {reasonCode}{(string.IsNullOrWhiteSpace(reasonDetail) ? string.Empty : $" - {reasonDetail.Trim()}")}. Routed to {routeTarget.DisplayName}. Requester={requesterId}.",
            ct);
    }

    public async Task RecordOutcomeAsync(
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string approverId,
        string outcome,
        string changeType,
        string? notes,
        CancellationToken ct)
    {
        await AddAsync(
            bookingId,
            transactionId,
            transactionRef,
            "ApprovalOutcome",
            requesterId,
            $"Approval outcome for {changeType}: {outcome}. Approver={approverId}.{(string.IsNullOrWhiteSpace(notes) ? string.Empty : $" Notes: {notes.Trim()}")}",
            ct);
    }

    private async Task AddAsync(
        string bookingId,
        string transactionId,
        string transactionRef,
        string eventType,
        string recipient,
        string body,
        CancellationToken ct)
    {
        await _dispatches.AddAsync(new NotificationDispatchRecord(
            Id: Guid.NewGuid().ToString("N"),
            BookingId: bookingId,
            TransactionId: transactionId,
            TransactionRef: transactionRef,
            EventType: eventType,
            SmsRequested: false,
            EmailRequested: true,
            SmsStatus: "Skipped",
            EmailStatus: "Recorded",
            OutcomeCode: "Recorded",
            FailureDetails: null,
            RecipientPhone: null,
            RecipientEmail: recipient,
            ProviderMessageId: null,
            MessageBody: body.Length > 3900 ? body[..3900] : body,
            LifecycleEventId: null,
            CorrelationId: null,
            CreatedUtc: DateTime.UtcNow,
            UpdatedUtc: DateTime.UtcNow), ct);

        await _uow.SaveChangesAsync(ct);
    }
}
