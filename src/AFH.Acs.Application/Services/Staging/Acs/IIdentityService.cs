using AFH.Acs.Recorder.Models.V1;

namespace AFH.Acs.Recorder.Services.Acs;


public interface IIdentityService
{
    Task<IssueTokenResult> IssueTokenAsync(CancellationToken ct = default);
}
