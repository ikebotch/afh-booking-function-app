namespace AFH.Acs.Application.Abstractions.Advisers;

public interface IAdviserInfoProvider
{
    Task<AdviserInfo?> GetByIdAsync(string adviserId, CancellationToken ct = default);
}
