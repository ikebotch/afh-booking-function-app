using System.Text.Json;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Options;
using AFH.Booking.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Governance;

public sealed class CalendarGovernanceService : ICalendarGovernanceService
{
    private const int DefaultCompanyBufferMinutes = 30;

    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ICalendarEventSnapshotRepository _snapshots;
    private readonly IOperationalIssueRepository _issues;
    private readonly IOperationalNotificationService _notifications;
    private readonly IUnitOfWork _uow;
    private readonly OutlookGovernanceOptions _options;
    private readonly IClock _clock;

    public CalendarGovernanceService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICalendarEventSnapshotRepository snapshots,
        IOperationalIssueRepository issues,
        IOperationalNotificationService notifications,
        IUnitOfWork uow,
        IClock clock,
        IOptions<OutlookGovernanceOptions> options)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _snapshots = snapshots;
        _issues = issues;
        _notifications = notifications;
        _uow = uow;
        _clock = clock;
        _options = options.Value;
    }

    public async Task HandleDeletedEventAsync(
        string adviserId,
        string providerEventId,
        string? correlationId,
        CancellationToken ct)
    {
        var hold = await _holds.GetByCalendarEventIdAsync(providerEventId, ct);
        if (hold is null)
            return;

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return;

        var transaction = await _transactions.GetAsync(slot.TransactionId, ct);
        if (transaction is null)
            return;

        var issue = new OperationalIssueRecord(
            Id: Guid.NewGuid().ToString("N"),
            IssueType: OutlookIssueTypes.Governance,
            Code: OutlookIssueCodes.DeletionAttemptDetected,
            Severity: "High",
            Status: OperationalIssueStatuses.ReconciliationRequired,
            DetectedUtc: _clock.UtcNow,
            BookingId: hold.Id,
            TransactionId: transaction.Id,
            TransactionRef: transaction.TransactionRef,
            AdviserId: adviserId,
            ProviderEventId: providerEventId,
            CorrelationId: correlationId,
            MetadataJson: JsonSerializer.Serialize(new
            {
                reconciliation = _options.AutoReconcileDeletedEvents ? "AutoRestoreRequested" : "ControlledReconciliationRequired",
                holdStatus = hold.Status.ToString(),
                slotId = slot.Id,
                slot.StartUtc,
                slot.EndUtc
            }),
            EscalationCount: 0,
            LastEscalatedUtc: null);

        await _issues.AddAsync(issue, ct);
        await NotifyAndEscalateAsync(issue, BuildAdviserMessage(hold.Id, OutlookIssueCodes.DeletionAttemptDetected), ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task HandleSnapshotAsync(
        string adviserId,
        string providerEventId,
        CalendarEventDetails evt,
        string? correlationId,
        CancellationToken ct)
    {
        var hold = await _holds.GetByCalendarEventIdAsync(providerEventId, ct);
        if (hold is null)
            return;

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return;

        var transaction = await _transactions.GetAsync(slot.TransactionId, ct);
        if (transaction is null)
            return;

        var latestSnapshot = await _snapshots.GetLatestAsync(adviserId, providerEventId, ct);
        var issuesToRecord = new List<OperationalIssueRecord>();

        var expectedShowAs = hold.Status == BookingHoldStatus.Confirmed ? BookingShowAs.Busy.ToString() : BookingShowAs.Tentative.ToString();
        if (!string.IsNullOrWhiteSpace(evt.ShowAs) &&
            !string.Equals(evt.ShowAs, expectedShowAs, StringComparison.OrdinalIgnoreCase))
        {
            issuesToRecord.Add(CreateIssue(
                OutlookIssueTypes.Hygiene,
                OutlookIssueCodes.IncorrectShowAs,
                "Medium",
                hold,
                transaction,
                adviserId,
                providerEventId,
                correlationId,
                new { expectedShowAs, actualShowAs = evt.ShowAs }));
        }

        if (!transaction.IsRemote && !evt.HasLocation)
        {
            issuesToRecord.Add(CreateIssue(
                OutlookIssueTypes.Hygiene,
                OutlookIssueCodes.MissingLocation,
                "Medium",
                hold,
                transaction,
                adviserId,
                providerEventId,
                correlationId,
                new { bookingMode = "InPerson" }));
        }

        if (evt.IsRecurring || !string.IsNullOrWhiteSpace(evt.RecurrencePattern))
        {
            issuesToRecord.Add(CreateIssue(
                OutlookIssueTypes.Hygiene,
                OutlookIssueCodes.InvalidRecurrencePattern,
                "High",
                hold,
                transaction,
                adviserId,
                providerEventId,
                correlationId,
                new { evt.IsRecurring, evt.RecurrencePattern }));
        }

        var expectedWindow = BuildExpectedWindow(slot, transaction);
        if (evt.StartUtc != expectedWindow.startUtc || evt.EndUtc != expectedWindow.endUtc)
        {
            issuesToRecord.Add(CreateIssue(
                OutlookIssueTypes.Governance,
                OutlookIssueCodes.EventTamperingDetected,
                "High",
                hold,
                transaction,
                adviserId,
                providerEventId,
                correlationId,
                new
                {
                    expectedStartUtc = expectedWindow.startUtc,
                    expectedEndUtc = expectedWindow.endUtc,
                    actualStartUtc = evt.StartUtc,
                    actualEndUtc = evt.EndUtc,
                    previousChangeKey = latestSnapshot?.ChangeKey,
                    evt.ChangeKey
                }));
        }

        if (issuesToRecord.Count == 0)
            return;

        foreach (var issue in issuesToRecord)
        {
            await _issues.AddAsync(issue, ct);
            await NotifyAndEscalateAsync(issue, BuildAdviserMessage(hold.Id, issue.Code), ct);
        }

        await _uow.SaveChangesAsync(ct);
    }

    private async Task NotifyAndEscalateAsync(OperationalIssueRecord issue, string adviserMessage, CancellationToken ct)
    {
        if (_options.AdviserNotificationsEnabled && !string.IsNullOrWhiteSpace(issue.AdviserId))
        {
            await _notifications.NotifyAdviserAsync(
                issue.AdviserId!,
                issue.BookingId ?? string.Empty,
                issue.TransactionId,
                issue.TransactionRef,
                issue.Code,
                adviserMessage,
                issue.CorrelationId,
                ct);
        }

        var sinceUtc = issue.DetectedUtc.AddHours(-Math.Max(1, _options.EscalationWindowHours));
        var recentCount = await _issues.CountRecentAsync(issue.AdviserId ?? string.Empty, issue.Code, sinceUtc, ct);
        if (recentCount + 1 < Math.Max(1, _options.EscalationThreshold) || _options.ManagerRecipients.Length == 0)
            return;

        var escalated = issue with
        {
            Status = OperationalIssueStatuses.Escalated,
            EscalationCount = issue.EscalationCount + 1,
            LastEscalatedUtc = _clock.UtcNow
        };

        await _issues.UpdateAsync(escalated, ct);
        await _notifications.NotifyManagersAsync(
            _options.ManagerRecipients,
            issue.BookingId ?? string.Empty,
            issue.TransactionId,
            issue.TransactionRef,
            $"{issue.Code}.Escalated",
            $"Escalation triggered for adviser mailbox hygiene issue {issue.Code}. BookingId={issue.BookingId}.",
            issue.CorrelationId,
            ct);
    }

    private static string BuildAdviserMessage(string bookingId, string issueCode)
        => $"Booking {bookingId} needs Outlook attention. Issue: {issueCode}. Please review the booking event in Outlook. Client-sensitive details are intentionally omitted.";

    private static (DateTime startUtc, DateTime endUtc) BuildExpectedWindow(BookingSlot slot, BookingTransaction transaction)
    {
        var travelMinutes = transaction.IsRemote ? 0 : Math.Max(0, slot.TravelMinutes ?? 0);
        var companyBufferMinutes = transaction.IsRemote ? 0 : Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes);
        var preMeetingMinutes = travelMinutes + companyBufferMinutes;
        var postMeetingMinutes = companyBufferMinutes;
        return (slot.StartUtc.AddMinutes(-preMeetingMinutes), slot.EndUtc.AddMinutes(postMeetingMinutes));
    }

    private OperationalIssueRecord CreateIssue(
        string issueType,
        string code,
        string severity,
        BookingHold hold,
        BookingTransaction transaction,
        string adviserId,
        string providerEventId,
        string? correlationId,
        object metadata)
    {
        return new OperationalIssueRecord(
            Id: Guid.NewGuid().ToString("N"),
            IssueType: issueType,
            Code: code,
            Severity: severity,
            Status: OperationalIssueStatuses.Open,
            DetectedUtc: _clock.UtcNow,
            BookingId: hold.Id,
            TransactionId: transaction.Id,
            TransactionRef: transaction.TransactionRef,
            AdviserId: adviserId,
            ProviderEventId: providerEventId,
            CorrelationId: correlationId,
            MetadataJson: JsonSerializer.Serialize(metadata),
            EscalationCount: 0,
            LastEscalatedUtc: null);
    }
}
