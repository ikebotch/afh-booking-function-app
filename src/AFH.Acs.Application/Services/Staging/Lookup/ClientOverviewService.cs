using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Helpers;
using AFH.Acs.Recorder.Infrastructure.Data.SharePointListFields;
using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using AFH.Integrations.Sharepoint.Services;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace AFH.Acs.Recorder.Services.Lookup;

public sealed class ClientOverviewService : IClientOverviewService
{

    private readonly SharepointService _sharepointService;
    private readonly SharePointConfigWrapper _spConfig;

    public ClientOverviewService(
        SharepointService sharepointService,
        IOptions<SharePointConfigWrapper> options)
    {
        _sharepointService = sharepointService ?? throw new ArgumentNullException(nameof(sharepointService));

        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _spConfig = options.Value ?? throw new InvalidOperationException("SharePointConfigs is not bound.");

        if (_spConfig.ClientOverviewListConfigs is null)
        {
            throw new InvalidOperationException(
                "SharePointConfigs:ClientOverviewListConfigs is not configured. " +
                "Ensure you have at least one advisor list configured under 'SharePointConfigs:ClientOverviewListConfigs'.");
        }
    }



    public async Task<IReadOnlyList<ClientOverviewSPDto>> GetClientOverviewDataAsync(
        string clientId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<ListItem> listItems;

        var clientConfig = _spConfig.ClientOverviewListConfigs;

        var expandFields = new[] { "fields" };

        try
        {
            listItems = await _sharepointService.GetListItems(
                clientConfig.SiteId,
                clientConfig.ListId,
                expandFields: expandFields
                );
            var aa = 0;
        }
        catch (Exception)
        {
            return Array.Empty<ClientOverviewSPDto>();
        }

        var dtos = listItems
            .Select(MapToDto)
            .ToList();

        return dtos;
    }

    public static ClientOverviewSPDto MapToDto(ListItem item)
    {
        var data = item.Fields.AdditionalData;

        return new ClientOverviewSPDto
        {
            ClientId = data.GetString(ClientOverviewFields.Title),
            LinkTitle = data.GetString(ClientOverviewFields.LinkTitle),
            LastMeetingSummary = data.GetString(ClientOverviewFields.LastMeetingSummary),
            ObjectivesAndGoals = data.GetString(ClientOverviewFields.ObjectivesAndGoals),
            RelationshipBuildingFacts = data.GetString(ClientOverviewFields.RelationshipBuildingFacts),
            RiskAndVulnerability = data.GetString(ClientOverviewFields.RiskAndVulnerability),
            MeetingDate = data.GetDateTime(ClientOverviewFields.Created)
        };
    }


}