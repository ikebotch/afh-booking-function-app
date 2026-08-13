using AFH.Booking.Application.Models.Calendar.Constants;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Models.Governance.Constants;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AFH.Booking.Application.Calendar;

public sealed class BookingShowAsRemediationService : IBookingShowAsRemediationService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RestoreLocks = new(StringComparer.Ordinal);

    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ICalendarGateway _calendar;
    private readonly IHoldWindowFactory _holdWindowFactory;
    private readonly IOperationalIssueRepository _issues;
    private readonly IUnitOfWork _uow;
    private readonly IBookingWorkflowNotificationAdapter? _notifications;
    private readonly ILogger<BookingShowAsRemediationService> _logger;

    public BookingShowAsRemediationService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICalendarGateway calendar,
        IHoldWindowFactory holdWindowFactory,
        IOperationalIssueRepository issues,
        IUnitOfWork uow,
        IBookingWorkflowNotificationAdapter? notifications = null,
        ILogger<BookingShowAsRemediationService>? logger = null)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _calendar = calendar;
        _holdWindowFactory = holdWindowFactory;
        _issues = issues;
        _uow = uow;
        _notifications = notifications;
        _logger = logger ?? NullLogger<BookingShowAsRemediationService>.Instance;
    }

    public async Task<Result<CalendarShowAsRemediationResult>> HandleAsync(string bookingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);

        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null)
            return Result<CalendarShowAsRemediationResult>.NotFound("Booking hold was not found.");

        if (hold.Status != BookingHoldStatus.Confirmed)
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Only confirmed bookings can be remediated or restored.",
                Errors.Conflict);

        if (string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Booking does not have a calendar event to remediate.",
                Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Booking slot was not found.",
                Errors.Conflict);

        var transaction = await _transactions.GetAsync(slot.TransactionId, ct);
        if (transaction is null)
            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Booking transaction was not found.",
                Errors.Conflict);

        var oldProviderEventId = hold.CalendarProviderEventId;
        var existingEvent = await GetExistingCalendarEventOrNullAsync(slot.AdviserId, oldProviderEventId, ct);
        if (existingEvent is null)
            return await RestoreMissingConfirmedEventAsync(hold, slot, transaction, oldProviderEventId, ct);

        await RestoreConfirmedEventDetailsAsync(
            hold,
            slot,
            transaction,
            CalendarCategoryConstants.ShowAsRemediation,
            ct);

        return Result<CalendarShowAsRemediationResult>.Ok(new CalendarShowAsRemediationResult
        {
            BookingId = hold.Id,
            EventId = hold.CalendarProviderEventId,
            ShowAs = "Busy",
            RemediatedUtc = DateTime.UtcNow
        });
    }

    public async Task<Result<CalendarProviderNotificationProcessingResult>> HandleProviderNotificationsAsync(
        CalendarProviderNotificationEnvelope envelope,
        CancellationToken ct)
    {
        var items = envelope.Value?.Where(x => x is not null).ToArray() ?? [];
        var results = new List<CalendarProviderNotificationItemResult>(items.Length);
        var corrected = 0;
        var restored = 0;
        var flagged = 0;
        var ignored = 0;

        foreach (var item in items)
        {
            var itemResult = await HandleProviderNotificationItemAsync(item, ct);
            results.Add(itemResult);

            switch (itemResult.Outcome)
            {
                case "Corrected":
                    corrected++;
                    break;
                case "Restored":
                    restored++;
                    break;
                case "FlaggedForOperations":
                    flagged++;
                    break;
                default:
                    ignored++;
                    break;
            }
        }

        return Result<CalendarProviderNotificationProcessingResult>.Ok(new CalendarProviderNotificationProcessingResult
        {
            Received = items.Length,
            Ignored = ignored,
            Corrected = corrected,
            Restored = restored,
            FlaggedForOperations = flagged,
            Items = results
        });
    }

    private async Task<CalendarProviderNotificationItemResult> HandleProviderNotificationItemAsync(
        CalendarProviderNotificationItem item,
        CancellationToken ct)
    {
        var changeType = item.ChangeType?.Trim() ?? string.Empty;
        var providerEventId = TryGetProviderEventId(item);
        if (string.IsNullOrWhiteSpace(providerEventId))
            return Ignored(item, null, null, "Provider event id was not present in the notification.");

        var hold = await _holds.GetByCalendarEventIdAsync(providerEventId, ct);
        if (hold is null)
            return Ignored(item, providerEventId, null, "Provider event is not managed by Booking.");

        if (hold.Status != BookingHoldStatus.Confirmed)
            return Ignored(item, providerEventId, hold.Id, "Booking is not confirmed.");

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return await FlagUnrecoverableAsync(item, providerEventId, hold, null, null, "Booking slot was not found.", ct);

        var transaction = await _transactions.GetAsync(slot.TransactionId, ct);
        if (transaction is null)
            return await FlagUnrecoverableAsync(item, providerEventId, hold, slot, null, "Booking transaction was not found.", ct);

        try
        {
            if (IsDeletion(changeType))
            {
                return await HandleDeletionNotificationAsync(item, hold, slot, transaction, providerEventId, changeType, ct);
            }

            var existingEvent = await GetExistingCalendarEventOrNullAsync(slot.AdviserId, providerEventId, ct);
            if (existingEvent is null)
            {
                return await HandleDeletionNotificationAsync(item, hold, slot, transaction, providerEventId, changeType, ct);
            }

            var differences = GetDifferences(existingEvent, slot).ToArray();
            if (differences.Length == 0)
                return Ignored(item, providerEventId, hold.Id, "Calendar event still matches Booking state.");

            var issueCode = differences.Length == 1 && differences[0] == "ShowAs"
                ? OutlookIssueCodes.IncorrectShowAs
                : OutlookIssueCodes.EventTamperingDetected;

            await RecordIssueAsync(
                issueCode,
                OperationalIssueStatuses.Open,
                hold,
                slot,
                transaction,
                providerEventId,
                null,
                $"Manual calendar edit detected: {string.Join(", ", differences)}.",
                ct);

            await RestoreConfirmedEventDetailsAsync(
                hold,
                slot,
                transaction,
                CalendarCategoryConstants.ShowAsRemediation,
                ct);
            await _uow.SaveChangesAsync(ct);

            await NotifyCalendarCorrectionAsync(
                BookingNotificationTypes.CalendarEventCorrected.Name,
                hold,
                slot,
                transaction,
                providerEventId,
                $"Manual calendar edit was corrected: {string.Join(", ", differences)}.",
                ct);

            return new CalendarProviderNotificationItemResult
            {
                ProviderEventId = providerEventId,
                BookingId = hold.Id,
                ChangeType = changeType,
                Outcome = "Corrected",
                Reason = string.Join(", ", differences)
            };
        }
        catch (Exception ex)
        {
            return await FlagUnrecoverableAsync(item, providerEventId, hold, slot, transaction, ex.Message, ct);
        }
    }

    private async Task<CalendarProviderNotificationItemResult> HandleDeletionNotificationAsync(
        CalendarProviderNotificationItem item,
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        string providerEventId,
        string changeType,
        CancellationToken ct)
    {
        var gate = RestoreLocks.GetOrAdd(hold.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var currentHold = await _holds.GetAsync(hold.Id, ct) ?? hold;
            if (currentHold.Status != BookingHoldStatus.Confirmed)
                return Ignored(item, providerEventId, currentHold.Id, "Booking is no longer confirmed.");

            if (!string.Equals(currentHold.CalendarProviderEventId, providerEventId, StringComparison.OrdinalIgnoreCase))
            {
                return Ignored(
                    item,
                    providerEventId,
                    currentHold.Id,
                    "Duplicate delete notification ignored because the booking now points at a different provider event.");
            }

            var previousRestore = await _issues.GetLatestAsync(
                slot.AdviserId,
                providerEventId,
                OutlookIssueCodes.CalendarEventMissingRestored,
                ct);
            if (previousRestore is not null)
            {
                return Ignored(
                    item,
                    providerEventId,
                    currentHold.Id,
                    "Duplicate delete notification ignored because this provider event was already restored.");
            }

            await RecordIssueAsync(
                OutlookIssueCodes.DeletionAttemptDetected,
                OperationalIssueStatuses.Open,
                currentHold,
                slot,
                transaction,
                providerEventId,
                null,
                "Manual deletion detected from calendar provider notification.",
                ct);

            var restoredResult = await RestoreMissingConfirmedEventAsync(currentHold, slot, transaction, providerEventId, ct);
            if (!restoredResult.IsSuccess)
            {
                await NotifyCalendarCorrectionAsync(
                    BookingNotificationTypes.CalendarEventCorrectionFailed.Name,
                    currentHold,
                    slot,
                    transaction,
                    providerEventId,
                    "Manual deletion could not be restored.",
                    ct);

                return new CalendarProviderNotificationItemResult
                {
                    ProviderEventId = providerEventId,
                    BookingId = currentHold.Id,
                    ChangeType = changeType,
                    Outcome = "FlaggedForOperations",
                    Reason = restoredResult.ErrorMessage
                };
            }

            await NotifyCalendarCorrectionAsync(
                BookingNotificationTypes.CalendarEventCorrected.Name,
                currentHold,
                slot,
                transaction,
                restoredResult.Value!.EventId,
                "Manual deletion was detected and the booking event was recreated.",
                ct);

            return new CalendarProviderNotificationItemResult
            {
                ProviderEventId = providerEventId,
                BookingId = currentHold.Id,
                ChangeType = changeType,
                Outcome = "Restored",
                Reason = "Manual deletion was restored."
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CalendarEventDetails?> GetExistingCalendarEventOrNullAsync(
        string calendarUserId,
        string providerEventId,
        CancellationToken ct)
    {
        try
        {
            return await _calendar.GetEventAsync(calendarUserId, providerEventId, ct);
        }
        catch (CalendarNotFoundException)
        {
            return null;
        }
    }

    private async Task<Result<CalendarShowAsRemediationResult>> RestoreMissingConfirmedEventAsync(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        string oldProviderEventId,
        CancellationToken ct)
    {
        try
        {
            var windows = _holdWindowFactory.Create(slot, transaction);
            var calendarTemplate = ConfirmedBookingTemplate.BuildConfirmedTemplate(
                slot,
                transaction,
                hold,
                windows,
                joinUrl: null,
                location: null,
                selfServiceLinks: null);

            var subject = string.IsNullOrWhiteSpace(transaction.MeetingType)
                ? "AFH Booking"
                : $"AFH Booking - {transaction.MeetingType}";

            var calendarEvent = BookingCalendarEvent.Create(
                userId: slot.AdviserId,
                externalId: $"booking:{hold.Id}",
                subject: subject,
                startUtc: slot.StartUtc,
                endUtc: slot.EndUtc,
                timezone: transaction.Timezone,
                isRemote: transaction.IsRemote,
                categories: CalendarCategoryConstants.MissingEventRestore,
                body: calendarTemplate.CalendarDescription,
                providerEventId: null,
                location: null,
                attendees: null,
                showAs: BookingShowAs.Busy);

            var newProviderEventId = await _calendar.CreateBookingEventAsync(calendarEvent, ct);
            if (string.IsNullOrWhiteSpace(newProviderEventId))
                throw new InvalidOperationException("Calendar restore created an event but did not return a provider event id.");

            hold.AttachCalendarEvent(newProviderEventId);
            await _holds.UpdateAsync(hold, ct);
            await RecordIssueAsync(
                OutlookIssueCodes.CalendarEventMissingRestored,
                OperationalIssueStatuses.Open,
                hold,
                slot,
                transaction,
                oldProviderEventId,
                newProviderEventId,
                failure: null,
                ct);
            await _uow.SaveChangesAsync(ct);

            return Result<CalendarShowAsRemediationResult>.Ok(new CalendarShowAsRemediationResult
            {
                BookingId = hold.Id,
                EventId = newProviderEventId,
                PreviousEventId = oldProviderEventId,
                ShowAs = "Busy",
                RestoredMissingEvent = true,
                RemediatedUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await RecordIssueAsync(
                OutlookIssueCodes.CalendarEventMissingRestoreFailed,
                OperationalIssueStatuses.ReconciliationRequired,
                hold,
                slot,
                transaction,
                oldProviderEventId,
                newProviderEventId: null,
                failure: ex.Message,
                ct);
            await _uow.SaveChangesAsync(ct);

            return Result<CalendarShowAsRemediationResult>.Fail(
                HttpStatusCode.Conflict,
                "Confirmed booking calendar event is missing and could not be restored.",
                OutlookIssueCodes.CalendarEventMissingRestoreFailed);
        }
    }

    private async Task RestoreConfirmedEventDetailsAsync(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        IEnumerable<string> categories,
        CancellationToken ct)
    {
        var windows = _holdWindowFactory.Create(slot, transaction);
        var calendarTemplate = ConfirmedBookingTemplate.BuildConfirmedTemplate(
            slot,
            transaction,
            hold,
            windows,
            joinUrl: null,
            location: null,
            selfServiceLinks: null);

        var subject = string.IsNullOrWhiteSpace(transaction.MeetingType)
            ? "AFH Booking"
            : $"AFH Booking - {transaction.MeetingType}";

        var update = BookingCalendarEvent.Update(
            userId: slot.AdviserId,
            showAs: BookingShowAs.Busy,
            providerEventId: hold.CalendarProviderEventId,
            body: calendarTemplate.CalendarDescription,
            categories: categories,
            subject: subject,
            startUtc: slot.StartUtc,
            endUtc: slot.EndUtc,
            timezone: transaction.Timezone,
            isRemote: transaction.IsRemote);

        await _calendar.UpdateBookingEventAsync(update, ct);
    }

    private async Task<CalendarProviderNotificationItemResult> FlagUnrecoverableAsync(
        CalendarProviderNotificationItem item,
        string providerEventId,
        BookingHold hold,
        BookingSlot? slot,
        BookingTransaction? transaction,
        string reason,
        CancellationToken ct)
    {
        if (slot is not null && transaction is not null)
        {
            await RecordIssueAsync(
                OutlookIssueCodes.ControlledReconciliationRequired,
                OperationalIssueStatuses.ReconciliationRequired,
                hold,
                slot,
                transaction,
                providerEventId,
                null,
                reason,
                ct);
            await _uow.SaveChangesAsync(ct);

            await NotifyCalendarCorrectionAsync(
                BookingNotificationTypes.CalendarEventCorrectionFailed.Name,
                hold,
                slot,
                transaction,
                providerEventId,
                reason,
                ct);
        }

        return new CalendarProviderNotificationItemResult
        {
            ProviderEventId = providerEventId,
            BookingId = hold.Id,
            ChangeType = item.ChangeType?.Trim() ?? string.Empty,
            Outcome = "FlaggedForOperations",
            Reason = reason
        };
    }

    private async Task RecordIssueAsync(
        string code,
        string status,
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        string oldProviderEventId,
        string? newProviderEventId,
        string? failure,
        CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            TriggerReason = code,
            OldProviderEventId = oldProviderEventId,
            NewProviderEventId = newProviderEventId,
            ActorType = "System",
            Failure = failure
        });

        await _issues.AddAsync(new OperationalIssueRecord(
            Id: Guid.NewGuid().ToString("N"),
            IssueType: OutlookIssueTypes.Governance,
            Code: code,
            Severity: failure is null ? "Information" : "Warning",
            Status: status,
            DetectedUtc: DateTime.UtcNow,
            BookingId: hold.Id,
            TransactionId: transaction.Id,
            TransactionRef: transaction.TransactionRef,
            AdviserId: slot.AdviserId,
            ProviderEventId: oldProviderEventId,
            CorrelationId: null,
            MetadataJson: metadata,
            EscalationCount: 0,
            LastEscalatedUtc: null), ct);
    }

    private async Task NotifyCalendarCorrectionAsync(
        string notificationType,
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        string providerEventId,
        string reason,
        CancellationToken ct)
    {
        if (_notifications is null)
            return;

        try
        {
            var outcome = await _notifications.RequestAsync(
                new BookingWorkflowNotificationRequest(
                    notificationType,
                    hold.Id,
                    LifecycleActors.System,
                    Array.Empty<BookingNotificationRecipient>(),
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = hold.Id,
                        ["transactionId"] = transaction.Id,
                        ["transactionRef"] = transaction.TransactionRef,
                        ["providerEventId"] = providerEventId,
                        ["adviserId"] = slot.AdviserId,
                        ["adviserName"] = slot.AdviserName,
                        ["meetingType"] = string.IsNullOrWhiteSpace(transaction.MeetingType) ? "N/A" : transaction.MeetingType,
                        ["startUtc"] = slot.StartUtc.ToString("O"),
                        ["endUtc"] = slot.EndUtc.ToString("O"),
                        ["when"] = $"{slot.StartUtc:O} - {slot.EndUtc:O}",
                        ["reason"] = reason,
                        ["correctionReason"] = reason,
                        ["IdempotencyKey"] = $"{notificationType}:{hold.Id}:{providerEventId}"
                    }),
                ct);

            if (outcome.Status == BookingWorkflowNotificationOutcomeStatuses.Succeeded)
            {
                _logger.LogInformation(
                    "Calendar correction notification published. NotificationType={NotificationType} BookingId={BookingId} ProviderEventId={ProviderEventId} RecipientCount={RecipientCount}",
                    notificationType,
                    hold.Id,
                    providerEventId,
                    outcome.RecipientCount);
                return;
            }

            _logger.LogWarning(
                "Calendar correction notification was not published. NotificationType={NotificationType} BookingId={BookingId} ProviderEventId={ProviderEventId} Status={Status} FailureCode={FailureCode} FailureMessage={FailureMessage}",
                notificationType,
                hold.Id,
                providerEventId,
                outcome.Status,
                outcome.FailureCode,
                outcome.FailureMessageSafe);
        }
        catch (Exception ex)
        {
            // Calendar correction must not fail because notification dispatch failed.
            _logger.LogWarning(
                ex,
                "Calendar correction notification threw during publish. NotificationType={NotificationType} BookingId={BookingId} ProviderEventId={ProviderEventId}",
                notificationType,
                hold.Id,
                providerEventId);
        }
    }

    private static IEnumerable<string> GetDifferences(CalendarEventDetails existingEvent, BookingSlot slot)
    {
        if (AsUtc(existingEvent.StartUtc) != AsUtc(slot.StartUtc))
            yield return "StartUtc";

        if (AsUtc(existingEvent.EndUtc) != AsUtc(slot.EndUtc))
            yield return "EndUtc";

        if (!string.Equals(existingEvent.ShowAs, "Busy", StringComparison.OrdinalIgnoreCase))
            yield return "ShowAs";
    }

    private static string? TryGetProviderEventId(CalendarProviderNotificationItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.ResourceData?.Id))
            return item.ResourceData.Id.Trim();

        if (string.IsNullOrWhiteSpace(item.Resource))
            return null;

        var segments = item.Resource.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var eventsIndex = Array.FindIndex(segments, x => string.Equals(x, "events", StringComparison.OrdinalIgnoreCase));
        if (eventsIndex >= 0 && eventsIndex + 1 < segments.Length)
            return Uri.UnescapeDataString(segments[eventsIndex + 1]);

        return segments.Length > 0 ? Uri.UnescapeDataString(segments[^1]) : null;
    }

    private static bool IsDeletion(string changeType)
        => changeType.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(x => string.Equals(x, "deleted", StringComparison.OrdinalIgnoreCase));

    private static CalendarProviderNotificationItemResult Ignored(
        CalendarProviderNotificationItem item,
        string? providerEventId,
        string? bookingId,
        string reason)
        => new()
        {
            ProviderEventId = providerEventId,
            BookingId = bookingId,
            ChangeType = item.ChangeType?.Trim() ?? string.Empty,
            Outcome = "Ignored",
            Reason = reason
        };

    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
