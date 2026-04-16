using System.Net.Http.Json;
using AFH.Acs.Application.Abstractions.Advisers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Infrastructure.Advisers;

public sealed class LocationAdviserInfoProvider(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<LocationAdviserInfoOptions> options,
    ILogger<LocationAdviserInfoProvider> logger) : IAdviserInfoProvider
{
    private const string CacheKeyPrefix = "location-adviser-info:";

    public async Task<AdviserInfo?> GetByIdAsync(string adviserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adviserId))
        {
            return null;
        }

        var resolvedAdviserId = adviserId.Trim();
        if (cache.TryGetValue(CacheKeyPrefix + resolvedAdviserId, out AdviserInfo? cached))
        {
            return cached;
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            logger.LogDebug("Location adviser info lookup skipped because Location:BaseUrl is not configured.");
            return null;
        }

        var path = settings.CoveragePath.Trim();
        var response = await httpClient.GetFromJsonAsync<LocationAdviserCoverageResponse>(
            AppendFunctionCode(path, settings.FunctionCode),
            ct);

        var adviser = response?.Advisers.FirstOrDefault(item =>
            string.Equals(item.Id, resolvedAdviserId, StringComparison.OrdinalIgnoreCase));

        if (adviser is null)
        {
            return null;
        }

        var mapped = new AdviserInfo
        {
            AdviserId = adviser.Id,
            DisplayName = adviser.Name,
            MailboxUserId = adviser.MailboxUserId
        };

        cache.Set(CacheKeyPrefix + resolvedAdviserId, mapped, settings.CacheDuration);
        return mapped;
    }

    private static string AppendFunctionCode(string path, string? functionCode)
    {
        if (string.IsNullOrWhiteSpace(functionCode))
        {
            return path;
        }

        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}code={Uri.EscapeDataString(functionCode)}";
    }

    private sealed class LocationAdviserCoverageResponse
    {
        public IReadOnlyList<LocationAdviserCoveragePoint> Advisers { get; init; } = [];
    }

    private sealed class LocationAdviserCoveragePoint
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? MailboxUserId { get; init; }
    }
}
