using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Models.V1;

namespace AFH.Acs.Recorder.Services.Interface;

public interface ILeadService
{
    Task<LeadListItemDto?> GetLeadAsync(
        string leadId,
        CancellationToken ct = default);

    Task<PagedResult<LeadListItemDto>> SearchLeadsAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct = default);
}