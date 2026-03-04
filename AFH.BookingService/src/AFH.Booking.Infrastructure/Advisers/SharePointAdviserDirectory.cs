using AFH.Booking.Application.Abstractions.Advisers;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Infrastructure.Composition;
using AFH.Booking.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace AFH.Booking.Infrastructure.Advisers;

public sealed class SharePointAdviserDirectory : IAdviserDirectory
{
    private readonly GraphServiceClient _graph;
    private readonly IOptions<SharePointOptions> _opts;
    private readonly ILogger<SharePointAdviserDirectory> _logger;

    public SharePointAdviserDirectory(
        ISharePointGraphClient spGraph,
        IOptions<SharePointOptions> opts,
        ILogger<SharePointAdviserDirectory> logger)
    {
        _graph = spGraph.Client;
        _opts = opts;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdviserDirectoryItem>> ListAsync(CancellationToken ct)
    {
        var o = _opts.Value;

        if (string.IsNullOrWhiteSpace(o.SiteId) || string.IsNullOrWhiteSpace(o.AdvisersListId))
            throw new InvalidOperationException("SharePoint SiteId and AdvisersListId must be configured.");

        _logger.LogInformation("Loading advisers from SharePoint siteId={SiteId} listId={ListId}", o.SiteId, o.AdvisersListId);
        _logger.LogInformation("SharePoint config SiteId='{SiteId}', ListId='{ListId}', Fields: adviserId='{AdviserIdField}' name='{NameField}'", o.SiteId, o.AdvisersListId, o.AdviserIdField, o.NameField);


        var page = await _graph
            .Sites[o.SiteId]
            .Lists[o.AdvisersListId]
            .Items
            .GetAsync(cfg =>
            {
                cfg.QueryParameters.Expand = new[] { "fields" };
                cfg.QueryParameters.Top = 999;
            }, ct);

        var items = page?.Value ?? [];
        var result = new List<AdviserDirectoryItem>(items.Count);

        foreach (var li in items)
        {
            var fields = li.Fields?.AdditionalData;
            if (fields is null) continue;

            string? adviserId = TryGet(fields, o.AdviserIdField);
            string? name = TryGet(fields, o.NameField);
            string? email = TryGet(fields, o.EmailField);
            string? region = TryGet(fields, o.RegionField);

            if (string.IsNullOrWhiteSpace(adviserId) || string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new AdviserDirectoryItem
            {
                AdviserId = adviserId,
                Name = name,
                Email = email,
                Region = region
            });
        }

        return result;
    }

    private static string? TryGet(IDictionary<string, object> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;
}
