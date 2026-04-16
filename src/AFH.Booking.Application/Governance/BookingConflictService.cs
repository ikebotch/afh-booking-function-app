using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Governance;

public sealed class BookingConflictService : IBookingConflictService
{
    private const int DefaultCompanyBufferMinutes = 30;

    private readonly ICalendarGateway _calendar;
    private readonly IOperationalIssueRepository _issues;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public BookingConflictService(
        ICalendarGateway calendar,
        IOperationalIssueRepository issues,
        IUnitOfWork uow,
        IClock clock)
    {
        _calendar = calendar;
        _issues = issues;
        _uow = uow;
        _clock = clock;
    }

    public async Task<BookingConflictCheckResult> EvaluateConfirmationConflictsAsync(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction transaction,
        string calendarUserId,
        CancellationToken ct)
    {
        var bufferStartUtc = slot.StartUtc.AddMinutes(-(transaction.IsRemote ? 0 : GetBufferMinutes(slot)));
        var bufferEndUtc = slot.EndUtc.AddMinutes(transaction.IsRemote ? 0 : Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes));

        var liveAvailability = await _calendar.CheckAvailabilityAsync(
            calendarUserId,
            bufferStartUtc,
            bufferEndUtc,
            transaction.Timezone,
            "ForceRefresh",
            ct);

        var relevantBlocks = liveAvailability.Conflicts
            .Where(x =>
                string.IsNullOrWhiteSpace(hold.CalendarProviderEventId) ||
                !string.Equals(x.ProviderEventId, hold.CalendarProviderEventId, StringComparison.OrdinalIgnoreCase))
            .Select(x => new
            {
                x.StartUtc,
                x.EndUtc,
                x.Subject,
                x.ProviderEventId
            })
            .ToList();

        if (relevantBlocks.Count == 0)
        {
            return new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>());
        }

        var details = new List<BookingConflictDetail>();
        foreach (var block in relevantBlocks)
        {
            var code = block.StartUtc < slot.EndUtc && block.EndUtc > slot.StartUtc
                ? Errors.BookingConflictDoubleBooked
                : Errors.BookingConflictBufferViolation;

            var message = code == Errors.BookingConflictDoubleBooked
                ? $"Adviser {slot.AdviserId} already has an overlapping Outlook event."
                : $"Adviser {slot.AdviserId} has an Outlook buffer conflict.";

            details.Add(new BookingConflictDetail(code, message, block.StartUtc, block.EndUtc, block.ProviderEventId));

            await _issues.AddAsync(new OperationalIssueRecord(
                Id: Guid.NewGuid().ToString("N"),
                IssueType: OutlookIssueTypes.Conflict,
                Code: code,
                Severity: "Error",
                Status: OperationalIssueStatuses.Open,
                DetectedUtc: _clock.UtcNow,
                BookingId: hold.Id,
                TransactionId: transaction.Id,
                TransactionRef: transaction.TransactionRef,
                AdviserId: slot.AdviserId,
                ProviderEventId: block.ProviderEventId,
                CorrelationId: null,
                MetadataJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    holdId = hold.Id,
                    slotId = slot.Id,
                    requestedStartUtc = slot.StartUtc,
                    requestedEndUtc = slot.EndUtc,
                    conflictStartUtc = block.StartUtc,
                    conflictEndUtc = block.EndUtc,
                    block.Subject
                }),
                EscalationCount: 0,
                LastEscalatedUtc: null), ct);
        }

        await _uow.SaveChangesAsync(ct);

        var first = details[0];
        return new BookingConflictCheckResult(true, first.Code, first.Message, details);
    }

    private static int GetBufferMinutes(BookingSlot slot)
    {
        var travelMinutes = Math.Max(0, slot.TravelMinutes ?? 0);
        var companyBufferMinutes = Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes);
        return travelMinutes + companyBufferMinutes;
    }
}
