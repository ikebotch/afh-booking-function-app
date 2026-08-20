using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Application.Services.Notifications;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Approvals;

public sealed class ApprovalNotificationService : IApprovalNotificationService
{
    private const string SubmittedTemplateKey = "adviser-request-submitted";
    private const string SubmittedSmsTemplateKey = "adviser-request-submitted-sms";
    private const string OutcomeTemplateKey = "adviser-request-outcome";
    private const string OutcomeSmsTemplateKey = "adviser-request-outcome-sms";
    private const string TemplateVersion = "v1";

    private readonly ILogger<ApprovalNotificationService> _logger;
    private readonly IBookingNotificationPublisher _publisher;
    private readonly IAdviserProfileProjectionRepository _advisers;
    private readonly IBookingNotificationPolicyProvider _policyProvider;
    private readonly IBookingNotificationRecipientResolver _recipientResolver;

    public ApprovalNotificationService(
        ILogger<ApprovalNotificationService> logger,
        IBookingNotificationPublisher publisher,
        IAdviserProfileProjectionRepository advisers,
        IBookingNotificationPolicyProvider policyProvider,
        IBookingNotificationRecipientResolver recipientResolver)
    {
        _logger = logger;
        _publisher = publisher;
        _advisers = advisers;
        _policyProvider = policyProvider;
        _recipientResolver = recipientResolver;
    }

    public async Task RecordRequestSubmittedAsync(
        ApprovalRouteTarget routeTarget,
        ApprovalWorkflowRecord approval,
        ApprovalBookingSnapshot booking,
        string requesterId,
        CancellationToken ct)
    {
        await AddAsync(
            approval.BookingId,
            approval.TransactionId,
            booking.Transaction.TransactionRef,
            "ApprovalSubmitted",
            routeTarget.TargetValue,
            $"Adviser request submitted for {approval.ChangeType}. Reason: {approval.ReasonCode}{(string.IsNullOrWhiteSpace(approval.ReasonDetail) ? string.Empty : $" - {approval.ReasonDetail.Trim()}")}. Routed to {routeTarget.DisplayName}. Requester={requesterId}.",
            ct);

        var data = await BuildApprovalDataAsync(
            approval,
            booking,
            requesterId,
            reviewerId: routeTarget.TargetValue,
            notificationStatus: "Pending",
            outcome: "Submitted",
            templateKey: SubmittedTemplateKey,
            smsTemplateKey: SubmittedSmsTemplateKey,
            idempotencyKey: $"approval-submitted:{approval.Id.Trim()}",
            ct);

        var adviser = await ResolveAdviserAsync(requesterId, ct);
        await PublishPolicyNotificationAsync(
            BookingNotificationTypes.AdviserRequestSubmitted,
            approval.Id,
            new BookingNotificationActor(
                ActorType: "Adviser",
                SourceApplication: "ApprovalWorkflow",
                Id: requesterId,
                DisplayName: data.GetValueOrDefault("adviserName", requesterId),
                Email: null),
            BuildRequestedRecipients(booking, adviser),
            data,
            ct);
    }

    public async Task RecordOutcomeAsync(
        ApprovalWorkflowRecord approval,
        ApprovalBookingSnapshot booking,
        string approverId,
        CancellationToken ct)
    {
        await AddAsync(
            approval.BookingId,
            approval.TransactionId,
            booking.Transaction.TransactionRef,
            "ApprovalOutcome",
            approval.RequesterId ?? approval.RequestedBy,
            $"Approval outcome for {approval.ChangeType}: {approval.Status}. Approver={approverId}.{(string.IsNullOrWhiteSpace(approval.ReviewNotes) ? string.Empty : $" Notes: {approval.ReviewNotes.Trim()}")}",
            ct);

        var requestId = approval.Id;
        var requesterId = approval.RequesterId ?? approval.RequestedBy;
        if (string.IsNullOrWhiteSpace(requesterId))
        {
            _logger.LogWarning(
                "Approval outcome notification skipped because requester id is missing. RequestId={RequestId} BookingId={BookingId} Outcome={Outcome}",
                requestId,
                approval.BookingId,
                approval.Status);
            return;
        }

        var adviser = await _advisers.GetAsync(requesterId.Trim(), ct);
        if (adviser is null)
        {
            _logger.LogWarning(
                "Approval outcome notification skipped because adviser could not be resolved. RequestId={RequestId} BookingId={BookingId} AdviserId={AdviserId} Outcome={Outcome}",
                requestId,
                approval.BookingId,
                requesterId,
                approval.Status);
            return;
        }

        var idempotencyKey = $"approval-outcome:{requestId.Trim()}:{approval.Status.Trim()}";
        var data = await BuildApprovalDataAsync(
            approval,
            booking,
            requesterId,
            approverId,
            approval.Status,
            approval.Status,
            OutcomeTemplateKey,
            OutcomeSmsTemplateKey,
            idempotencyKey,
            ct);

        await PublishPolicyNotificationAsync(
            BookingNotificationTypes.AdviserRequestOutcome,
            string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId.Trim(),
            new BookingNotificationActor(
                ActorType: "Manager",
                SourceApplication: "ApprovalWorkflow",
                Id: approverId,
                DisplayName: approverId,
                Email: null),
            BuildRequestedRecipients(booking, adviser),
            data,
            ct);
    }

    private async Task PublishPolicyNotificationAsync(
        BookingNotificationType notificationType,
        string correlationId,
        BookingNotificationActor actor,
        IReadOnlyList<BookingNotificationRecipient> requestedRecipients,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        try
        {
            var policy = await _policyProvider.GetAsync(BookingNotificationTypes.SourceApplication, notificationType, ct);
            if (!policy.Enabled)
            {
                _logger.LogInformation(
                    "Approval notification skipped because policy is disabled. NotificationType={NotificationType} CorrelationId={CorrelationId}",
                    notificationType.Name,
                    correlationId);
                return;
            }

            var recipients = await _recipientResolver.ResolveAsync(policy, requestedRecipients, data, ct);
            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "Approval notification skipped because no recipients resolved. NotificationType={NotificationType} CorrelationId={CorrelationId}",
                    notificationType.Name,
                    correlationId);
                return;
            }

            var notification = new BookingNotificationRequest(
                notificationType,
                string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim(),
                actor,
                recipients,
                data);

            await _publisher.PublishAsync(notification, ct);
            _logger.LogInformation(
                "Approval notification published. NotificationType={NotificationType} CorrelationId={CorrelationId} RecipientCount={RecipientCount} IdempotencyKey={IdempotencyKey}",
                notificationType.Name,
                correlationId,
                recipients.Count,
                data.GetValueOrDefault("IdempotencyKey"));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Approval notification publish failed after approval state was recorded. NotificationType={NotificationType} CorrelationId={CorrelationId}",
                notificationType.Name,
                correlationId);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> BuildApprovalDataAsync(
        ApprovalWorkflowRecord approval,
        ApprovalBookingSnapshot booking,
        string requesterId,
        string reviewerId,
        string notificationStatus,
        string outcome,
        string templateKey,
        string smsTemplateKey,
        string idempotencyKey,
        CancellationToken ct)
    {
        var adviser = await ResolveAdviserAsync(requesterId, ct);
        var adviserName = FirstNonEmpty(adviser?.DisplayName, booking.Slot.AdviserName, requesterId);
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RequestId"] = approval.Id,
            ["requestId"] = approval.Id,
            ["approvalRequestId"] = approval.Id,
            ["requestReference"] = approval.Reference ?? string.Empty,
            ["BookingId"] = approval.BookingId,
            ["bookingId"] = approval.BookingId,
            ["TransactionId"] = approval.TransactionId,
            ["transactionId"] = approval.TransactionId,
            ["TransactionRef"] = booking.Transaction.TransactionRef,
            ["transactionRef"] = booking.Transaction.TransactionRef,
            ["AdviserId"] = requesterId,
            ["adviserId"] = requesterId,
            ["AdviserName"] = adviserName,
            ["adviserName"] = adviserName,
            ["Reviewer"] = reviewerId,
            ["reviewer"] = reviewerId,
            ["Outcome"] = outcome,
            ["outcome"] = outcome,
            ["Status"] = notificationStatus,
            ["status"] = notificationStatus,
            ["ChangeType"] = approval.ChangeType,
            ["changeType"] = approval.ChangeType,
            ["IdempotencyKey"] = idempotencyKey,
            ["TemplateKey:Email"] = templateKey,
            ["TemplateVersion:Email"] = TemplateVersion,
            ["TemplateKey:Sms"] = smsTemplateKey,
            ["TemplateVersion:Sms"] = TemplateVersion,
            ["greetingName"] = "there",
            ["note"] = BuildNotificationNote(approval, outcome),
            ["decisionNotes"] = approval.ReviewNotes ?? string.Empty,
            ["DecisionNotes"] = approval.ReviewNotes ?? string.Empty,
            ["clientName"] = booking.Transaction.ClientName ?? approval.ClientName ?? string.Empty,
            ["clientEmail"] = booking.Transaction.ClientEmail ?? string.Empty
        };

        BookingNotificationPayloadFields.AddStandardBookingFields(
            data,
            booking.Transaction,
            booking.Slot,
            notificationStatus);

        AddIfPresent(data, "ReasonCode", approval.ReasonCode);
        AddIfPresent(data, "reasonCode", approval.ReasonCode);
        AddIfPresent(data, "ReasonDetail", approval.ReasonDetail);
        AddIfPresent(data, "reasonDetail", approval.ReasonDetail);
        return data;
    }

    private async Task<AdviserProfileProjectionRecord?> ResolveAdviserAsync(string requesterId, CancellationToken ct)
        => string.IsNullOrWhiteSpace(requesterId)
            ? null
            : await _advisers.GetAsync(requesterId.Trim(), ct);

    private static IReadOnlyList<BookingNotificationRecipient> BuildRequestedRecipients(
        ApprovalBookingSnapshot booking,
        AdviserProfileProjectionRecord? adviser = null)
    {
        var recipients = new List<BookingNotificationRecipient>();
        if (!string.IsNullOrWhiteSpace(booking.Transaction.ClientEmail))
        {
            recipients.Add(new BookingNotificationRecipient(
                BookingNotificationRecipientTypes.Client,
                booking.Transaction.ClientName,
                booking.Transaction.ClientEmail,
                PreferredChannels: [BookingNotificationChannel.Email]));
        }

        if (adviser is not null && !string.IsNullOrWhiteSpace(adviser.MailboxUserId))
        {
            recipients.Add(new BookingNotificationRecipient(
                BookingNotificationRecipientTypes.Adviser,
                string.IsNullOrWhiteSpace(adviser.DisplayName) ? booking.Slot.AdviserName : adviser.DisplayName,
                adviser.MailboxUserId,
                PreferredChannels: [BookingNotificationChannel.Email]));
        }

        return recipients;
    }

    private static string BuildNotificationNote(ApprovalWorkflowRecord approval, string outcome)
        => $"Adviser {approval.ChangeType} request {outcome.ToLowerInvariant()}. Reason: {approval.ReasonCode}{(string.IsNullOrWhiteSpace(approval.ReasonDetail) ? string.Empty : $" - {approval.ReasonDetail.Trim()}")}.";

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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
