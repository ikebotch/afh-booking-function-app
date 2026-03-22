using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Http;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class AdviserDirectorySyncService : IAdviserDirectorySyncService
{
    private readonly HttpClient _http;
    private readonly AdviserDirectoryOptions _options;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IClock _clock;

    public AdviserDirectorySyncService(
        HttpClient http,
        IOptions<AdviserDirectoryOptions> options,
        IAdviserProfileProjectionRepository profiles,
        IClock clock)
    {
        _http = http;
        _options = options.Value;
        _profiles = profiles;
        _clock = clock;
    }

    public async Task<AdviserDirectorySyncResult> SyncAsync(CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new AdviserDirectorySyncResult
            {
                SyncedAtUtc = _clock.UtcNow,
                SyncedCount = 0,
                Source = "disabled"
            };
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var path = _options.CoverageEndpointPath.StartsWith('/') ? _options.CoverageEndpointPath : "/" + _options.CoverageEndpointPath;
        var url = baseUrl + path;
        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
        {
            var sep = url.Contains('?') ? "&" : "?";
            url += $"{sep}code={Uri.EscapeDataString(_options.FunctionKey)}";
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            req.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var payload = await ReadCoverageAsync(res, ct);
        var now = _clock.UtcNow;

        var records = payload
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new AdviserProfileProjectionRecord
            {
                AdviserId = x.Id.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(x.Name) ? x.Id.Trim() : x.Name.Trim(),
                Region = x.Region?.Trim() ?? string.Empty,
                HomePostcode = x.Postcode?.Trim() ?? string.Empty,
                IsActive = true,
                Rating = 0d,
                Skills = Array.Empty<string>(),
                CoverageRadiusMiles = x.RadiusMiles > 0 ? x.RadiusMiles : null,
                MaxTravelTimeMinutes = x.MaxTravelTimeMinutes > 0 ? x.MaxTravelTimeMinutes : null,
                LastSyncedUtc = now,
                SourceVersion = now.ToString("O")
            })
            .ToList();

        await _profiles.UpsertRangeAsync(records, ct);
        return new AdviserDirectorySyncResult
        {
            SyncedAtUtc = now,
            SyncedCount = records.Count,
            Source = url
        };
    }

    private static async Task<IReadOnlyList<CoverageAdviserItem>> ReadCoverageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<CoverageData>>(json, options);
            if (envelope?.Data?.Advisers is { Count: > 0 })
                return envelope.Data.Advisers;

            var data = JsonSerializer.Deserialize<CoverageData>(json, options);
            if (data?.Advisers is { Count: > 0 })
                return data.Advisers;
        }
        catch
        {
            // ignored by design; return empty list
        }

        return [];
    }

    private sealed class CoverageData
    {
        public List<CoverageAdviserItem> Advisers { get; set; } = [];
    }

    private sealed class CoverageAdviserItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? Postcode { get; set; }
        public int MaxTravelTimeMinutes { get; set; }
        public double RadiusMiles { get; set; }
    }
}
