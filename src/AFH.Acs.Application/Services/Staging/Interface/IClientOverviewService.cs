using AFH.Acs.Recorder.DTOs;

namespace AFH.Acs.Recorder.Services.Interface;

public interface IClientOverviewService
{
  

    Task<IReadOnlyList<ClientOverviewSPDto>> GetClientOverviewDataAsync(string clientId,
        CancellationToken ct = default);
}