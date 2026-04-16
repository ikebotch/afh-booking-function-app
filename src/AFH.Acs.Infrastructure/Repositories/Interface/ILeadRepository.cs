using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;

public interface ILeadRepository
{
    Task<(IReadOnlyList<LeadEntity> Items, int TotalCount)> ListAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<LeadEntity?> GetByIdAsync(
        string leadId,
        CancellationToken ct = default);
}
