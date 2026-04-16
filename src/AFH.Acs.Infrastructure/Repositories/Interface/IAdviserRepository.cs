using AFH.Acs.Recorder.Infrastructure.Data.Entities;

namespace AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;

public interface IAdviserRepository
{
    Task<IReadOnlyList<AdviserEntity>> ListAsync(
        string? region,
        bool leadTechOnly,
        CancellationToken ct = default);

    Task<AdviserEntity?> GetByIdAsync(
        string adviserId,
        CancellationToken ct = default);
}