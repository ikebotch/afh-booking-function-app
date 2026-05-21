using AFH.Booking.Application.Models.Governance;

namespace AFH.Booking.Application.Abstractions.Governance;

public interface IOperationalIssueRepository
{
    Task AddAsync(OperationalIssueRecord record, CancellationToken ct);
    Task<OperationalIssueRecord?> GetLatestAsync(string adviserId, string providerEventId, string code, CancellationToken ct);
    Task<int> CountRecentAsync(string adviserId, string code, DateTime sinceUtc, CancellationToken ct);
    Task UpdateAsync(OperationalIssueRecord record, CancellationToken ct);
}
