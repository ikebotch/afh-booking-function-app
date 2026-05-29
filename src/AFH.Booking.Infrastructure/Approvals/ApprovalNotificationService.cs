using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class ApprovalNotificationService : IApprovalNotificationService
{
    private readonly ILogger<ApprovalNotificationService> _logger;

    public ApprovalNotificationService(ILogger<ApprovalNotificationService> logger)
    {
        _logger = logger;
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
        _logger.LogInformation(
            "Approval notification event recorded. BookingId={BookingId} TransactionId={TransactionId} TransactionRef={TransactionRef} EventType={EventType} Recipient={Recipient} Body={Body}",
            bookingId,
            transactionId,
            transactionRef,
            eventType,
            recipient,
            body.Length > 3900 ? body[..3900] : body);

        await Task.CompletedTask;
    }
}
