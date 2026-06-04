using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Models.Notifications;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Approvals;

public sealed class ApprovalNotificationService : IApprovalNotificationService
{
    private const string OutcomeTemplateKey = "adviser-request-outcome";
    private const string OutcomeSmsTemplateKey = "adviser-request-outcome-sms";
    private const string OutcomeTemplateVersion = "v1";

    private readonly ILogger<ApprovalNotificationService> _logger;
    private readonly IBookingNotificationPublisher _publisher;
    private readonly IAdviserProfileProjectionRepository _advisers;

    public ApprovalNotificationService(
        ILogger<ApprovalNotificationService> logger,
        IBookingNotificationPublisher publisher,
        IAdviserProfileProjectionRepository advisers)
    {
        _logger = logger;
        _publisher = publisher;
        _advisers = advisers;
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
        string requestId,
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string approverId,
        string outcome,
        string changeType,
        string? reasonCode,
        string? reasonDetail,
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

        if (string.IsNullOrWhiteSpace(requesterId))
        {
            _logger.LogWarning(
                "Approval outcome notification skipped because requester id is missing. RequestId={RequestId} BookingId={BookingId} Outcome={Outcome}",
                requestId,
                bookingId,
                outcome);
            return;
        }

        var adviser = await _advisers.GetAsync(requesterId.Trim(), ct);
        if (adviser is null || string.IsNullOrWhiteSpace(adviser.MailboxUserId))
        {
            _logger.LogWarning(
                "Approval outcome notification skipped because adviser email could not be resolved. RequestId={RequestId} BookingId={BookingId} AdviserId={AdviserId} Outcome={Outcome}",
                requestId,
                bookingId,
                requesterId,
                outcome);
            return;
        }

        var idempotencyKey = $"approval-outcome:{requestId.Trim()}:{outcome.Trim()}";
        var notification = new BookingNotificationRequest(
            BookingNotificationTypes.AdviserRequestOutcome,
            string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId.Trim(),
            new BookingNotificationActor(
                ActorType: "Manager",
                SourceApplication: "ApprovalWorkflow",
                Id: approverId,
                DisplayName: approverId,
                Email: null),
            [
                new BookingNotificationRecipient(
                    RecipientType: "Adviser",
                    DisplayName: string.IsNullOrWhiteSpace(adviser.DisplayName) ? requesterId : adviser.DisplayName,
                    Email: adviser.MailboxUserId,
                    PreferredChannels: [BookingNotificationChannel.Email])
            ],
            BuildOutcomeData(
                requestId,
                bookingId,
                transactionId,
                transactionRef,
                requesterId,
                approverId,
                outcome,
                changeType,
                reasonCode,
                reasonDetail,
                notes,
                idempotencyKey));

        try
        {
            await _publisher.PublishAsync(notification, ct);
            _logger.LogInformation(
                "Approval outcome notification published. RequestId={RequestId} BookingId={BookingId} AdviserId={AdviserId} Outcome={Outcome} IdempotencyKey={IdempotencyKey}",
                requestId,
                bookingId,
                requesterId,
                outcome,
                idempotencyKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Approval outcome notification publish failed after approval decision was recorded. RequestId={RequestId} BookingId={BookingId} AdviserId={AdviserId} Outcome={Outcome}",
                requestId,
                bookingId,
                requesterId,
                outcome);
        }
    }

    private static IReadOnlyDictionary<string, string> BuildOutcomeData(
        string requestId,
        string bookingId,
        string transactionId,
        string transactionRef,
        string requesterId,
        string approverId,
        string outcome,
        string changeType,
        string? reasonCode,
        string? reasonDetail,
        string? notes,
        string idempotencyKey)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RequestId"] = requestId,
            ["requestId"] = requestId,
            ["BookingId"] = bookingId,
            ["bookingId"] = bookingId,
            ["TransactionId"] = transactionId,
            ["transactionId"] = transactionId,
            ["TransactionRef"] = transactionRef,
            ["transactionRef"] = transactionRef,
            ["AdviserId"] = requesterId,
            ["adviserId"] = requesterId,
            ["Reviewer"] = approverId,
            ["reviewer"] = approverId,
            ["Outcome"] = outcome,
            ["outcome"] = outcome,
            ["Status"] = outcome,
            ["status"] = outcome,
            ["ChangeType"] = changeType,
            ["changeType"] = changeType,
            ["IdempotencyKey"] = idempotencyKey,
            ["TemplateKey:Email"] = OutcomeTemplateKey,
            ["TemplateVersion:Email"] = OutcomeTemplateVersion,
            ["TemplateKey:Sms"] = OutcomeSmsTemplateKey,
            ["TemplateVersion:Sms"] = OutcomeTemplateVersion
        };

        AddIfPresent(data, "ReasonCode", reasonCode);
        AddIfPresent(data, "reasonCode", reasonCode);
        AddIfPresent(data, "ReasonDetail", reasonDetail);
        AddIfPresent(data, "reasonDetail", reasonDetail);
        AddIfPresent(data, "DecisionNotes", notes);
        AddIfPresent(data, "decisionNotes", notes);
        return data;
    }

    private static void AddIfPresent(
        IDictionary<string, string> data,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            data[key] = value.Trim();
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
