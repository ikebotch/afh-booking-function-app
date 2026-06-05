using AFH.Booking.Application.Models.Calendar.Constants;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Models.Governance.Constants;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using System.Text.Json;

namespace AFH.Booking.Application.Calendar;

public sealed class BookingShowAsRemediationService : IBookingShowAsRemediationService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ICalendarGateway _calendar;
    private readonly IHoldWindowFactory _holdWindowFactory;
    private readonly IOperationalIssueRepository _issues;
    private readonly IUnitOfWork _uow;

    public BookingShowAsRemediationService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ICalendarGateway calendar,
        IHoldWindowFactory holdWindowFactory,
        IOperationalIssueRepository issues,
        IUnitOfWork uow)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _calendar = calendar;
        _holdWindowFactory = holdWindowFactory;
        _issues = issues;
        _uow = uow;
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

        var update = BookingCalendarEvent.Update(
            userId: slot.AdviserId,
            showAs: BookingShowAs.Busy,
            providerEventId: hold.CalendarProviderEventId,
            body: null,
            categories: CalendarCategoryConstants.ShowAsRemediation);

        await _calendar.UpdateBookingEventAsync(update, ct);

        return Result<CalendarShowAsRemediationResult>.Ok(new CalendarShowAsRemediationResult
        {
            BookingId = hold.Id,
            EventId = hold.CalendarProviderEventId,
            ShowAs = "Busy",
            RemediatedUtc = DateTime.UtcNow
        });
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
            TriggerReason = "CalendarEventMissingRestored",
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
}
