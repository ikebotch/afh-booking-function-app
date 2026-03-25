using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Abstractions.Governance;

public interface ICalendarGovernanceService
{
    Task HandleDeletedEventAsync(
        string adviserId,
        string providerEventId,
        string? correlationId,
        CancellationToken ct);

    Task HandleSnapshotAsync(
        string adviserId,
        string providerEventId,
        CalendarEventDetails evt,
        string? correlationId,
        CancellationToken ct);
}
