using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using Amazon.Auth.AccessControlPolicy;
using Grpc.Core;
using System.Drawing;

namespace AFH.Acs.Recorder.Services.Lookup;

public sealed class LeadService : ILeadService
{
    private readonly ILeadRepository _repo;

    public LeadService(ILeadRepository repo)
    {
        _repo = repo;
    }

    public async Task<LeadListItemDto?> GetLeadAsync(
        string leadId,
        CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(leadId, ct).ConfigureAwait(false);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<PagedResult<LeadListItemDto>> SearchLeadsAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (entities, total) = await _repo.ListAsync(query, page, pageSize, ct)
                                           .ConfigureAwait(false);

        var items = entities.Select(MapToDto).ToList();

        return new PagedResult<LeadListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static LeadListItemDto MapToDto(LeadEntity e)
        => new LeadListItemDto
        {
            LeadId = e.LeadId,

            ClientName = e.ClientName,
            Email = e.ClientEmail,
            //Region = e.Region,
            //Source = e.Source
        };
}