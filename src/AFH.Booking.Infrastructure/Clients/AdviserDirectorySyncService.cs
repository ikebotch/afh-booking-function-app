using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class AdviserDirectorySyncService : IAdviserDirectorySyncService
{
    private const string SyncCursorKey = "adviser_directory_last_sync_utc";
    private readonly HttpClient _http;
    private readonly AdviserDirectoryOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IIntegrationSyncStateRepository _syncState;
    private readonly IClock _clock;

    public AdviserDirectorySyncService(
        HttpClient http,
        IOptions<AdviserDirectoryOptions> options,
        IInternalServiceAuthenticator authenticator,
        IAdviserProfileProjectionRepository profiles,
        IIntegrationSyncStateRepository syncState,
        IClock clock)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _profiles = profiles;
        _syncState = syncState;
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

        DateTime? sinceUtc = null;
        var stateValue = await _syncState.GetValueAsync(SyncCursorKey, ct);
        if (!string.IsNullOrWhiteSpace(stateValue) &&
            DateTime.TryParse(stateValue, out var parsed))
        {
            sinceUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            var sepSince = url.Contains('?') ? "&" : "?";
            url += $"{sepSince}sinceUtc={Uri.EscapeDataString(sinceUtc.Value.ToString("O"))}";
        }
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        _authenticator.Apply(req, _options.InternalToken);

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
                MailboxUserId = FirstNonEmpty(
                    x.MailboxUserId,
                    x.Mailbox,
                    x.UserId,
                    x.Email,
                    x.AdviserEmail,
                    x.PrincipalName,
                    x.Id),
                Region = x.Region?.Trim() ?? string.Empty,
                HomePostcode = x.Postcode?.Trim() ?? string.Empty,
                IsActive = x.IsActive ?? true,
                Rating = x.Rating ?? 0d,
                Skills = x.Skills?
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                    ?? Array.Empty<string>(),
                CoverageRadiusMiles = x.RadiusMiles > 0 ? x.RadiusMiles : null,
                MaxTravelTimeMinutes = x.MaxTravelTimeMinutes > 0 ? x.MaxTravelTimeMinutes : null,
                LastSyncedUtc = now,
                SourceVersion = now.ToString("O")
            })
            .ToList();

        await _profiles.UpsertRangeAsync(records, ct);

        await _syncState.UpsertValueAsync(SyncCursorKey, now.ToString("O"), now, ct);
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
        public string? MailboxUserId { get; set; }
        public string? UserId { get; set; }
        public string? Mailbox { get; set; }
        public string? Email { get; set; }
        public string? AdviserEmail { get; set; }
        public string? PrincipalName { get; set; }
        public string? Region { get; set; }
        public string? Postcode { get; set; }
        public bool? IsActive { get; set; }
        public List<string>? Skills { get; set; }
        public double? Rating { get; set; }
        public int MaxTravelTimeMinutes { get; set; }
        public double RadiusMiles { get; set; }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
