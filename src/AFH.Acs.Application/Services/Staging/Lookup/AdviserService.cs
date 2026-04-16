using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Helpers;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Data.SharePointListFields;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using AFH.Integrations.Sharepoint.Services;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace AFH.Acs.Recorder.Services.Lookup;

public sealed class AdviserService : IAdviserService
{
    private readonly IAdviserRepository _repo;
    private readonly SharepointService _sharepointService;
    private readonly SharePointConfigWrapper _spConfig;

    public AdviserService(
        IAdviserRepository repo,
        SharepointService sharepointService,
        IOptions<SharePointConfigWrapper> options)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _sharepointService = sharepointService ?? throw new ArgumentNullException(nameof(sharepointService));

        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _spConfig = options.Value ?? throw new InvalidOperationException("SharePointConfigs is not bound.");

        if (_spConfig.AdvisorListConfigs is null)
        {
            throw new InvalidOperationException(
                "SharePointConfigs:AdvisorListConfigs is not configured. " +
                "Ensure you have at least one advisor list configured under 'SharePointConfigs:AdvisorListConfigs'.");
        }
    }

    
    public async Task<IReadOnlyList<AdviserListItemDto>> SearchAdvisersAsync(
        string? region,
        bool leadTechOnly,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var entities = await _repo.ListAsync(region, leadTechOnly, ct)
                                  .ConfigureAwait(false);

        return entities.Select(MapToDto).ToList();
    }

     public async Task<AdviserListItemDto?> GetAdviserAsync(
        string adviserId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var entity = await _repo.GetByIdAsync(adviserId, ct)
                                .ConfigureAwait(false);

        return entity is null ? null : MapToDto(entity);
    }


    public async Task<IReadOnlyList<AdviserListItemDto>> SearchAdvisersFromSharePointAsync(
        string? region,
        bool leadTechOnly,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<ListItem> listItems;

        var advisorConfig = _spConfig.AdvisorListConfigs;

        var expandFields = new[] { "fields" };
        string? regionFilter = null;

        if (!string.IsNullOrWhiteSpace(region))
        {
            // field_10 = Region column in SharePoint (per your mapping)
            regionFilter = $"fields/field_10 eq '{region}'";
        }

        try
        {
            listItems = await _sharepointService.GetListItems(
                advisorConfig.SiteId,
                advisorConfig.ListId,
                null,
                expandFields,
                regionFilter);
        }
        catch (Exception)
        {
            // If you wire in ILogger<AdviserService>, log here.
            return Array.Empty<AdviserListItemDto>();
        }

        var dtos = listItems
            .Select(MapToDto)
            .Where(d => !leadTechOnly || d.IsLeadTechAdviser)
            .ToList();

        return dtos;
    }



    public static AdviserListItemDto MapToDto(ListItem item)
    {
        var data = item.Fields.AdditionalData;

        return new AdviserListItemDto
        {
            AdviserId = data.GetString(AdviserFields.Email),
            Name = data.GetString(AdviserFields.Title),
            Email = data.GetString(AdviserFields.Email),
            Region = data.GetString(AdviserFields.Region),
            IsLeadTechAdviser = data.GetBoolEquals(AdviserFields.LeadSource, AdviserFields.LeadTechValue)
        };
    }

    private static AdviserListItemDto MapToDto(AdviserEntity a)
        => new AdviserListItemDto
        {
            AdviserId = a.AdviserId ?? a.Email,
            Name = a.FullName,
            Email = a.Email,
            Region = a.Region,
            IsLeadTechAdviser = a.LeadTechFlag
        };
}