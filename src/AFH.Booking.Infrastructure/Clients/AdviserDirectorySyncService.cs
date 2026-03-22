using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class AdviserDirectorySyncService : IAdviserDirectorySyncService
{
    private const string SyncCursorKey = "adviser_directory_last_sync_utc";
    private readonly HttpClient _http;
    private readonly AdviserDirectoryOptions _options;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IIntegrationSyncStateRepository _syncState;
    private readonly IClock _clock;
    private readonly ICreateSubscriptionHandler _createSubscription;
    private readonly ICalendarSubscriptionRepository _subscriptions;
    private readonly ILogger<AdviserDirectorySyncService> _logger;

    public AdviserDirectorySyncService(
        HttpClient http,
        IOptions<AdviserDirectoryOptions> options,
        IAdviserProfileProjectionRepository profiles,
        IIntegrationSyncStateRepository syncState,
        IClock clock,
        ICreateSubscriptionHandler createSubscription,
        ICalendarSubscriptionRepository subscriptions,
        ILogger<AdviserDirectorySyncService> logger)
    {
        _http = http;
        _options = options.Value;
        _profiles = profiles;
        _syncState = syncState;
        _clock = clock;
        _createSubscription = createSubscription;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task<AdviserDirectorySyncResult> SyncAsync(CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new AdviserDirectorySyncResult
            {
                SyncedAtUtc = _clock.UtcNow,
                SyncedCount = 0,
                MailboxesDetected = 0,
                SubscriptionsCreatedOrRenewed = 0,
                SubscriptionsSkipped = 0,
                SubscriptionFailures = 0,
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

        var mailboxUserIds = payload
            .Select(ResolveMailboxUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        var renewBeforeUtc = nowUtc.AddMinutes(Math.Max(15, _options.SubscriptionRenewalLeadMinutes));
        var createdOrRenewed = 0;
        var skipped = 0;
        var failures = 0;

        foreach (var mailboxUserId in mailboxUserIds)
        {
            var existing = await _subscriptions.GetLatestByUserIdAsync(mailboxUserId!, ct);
            if (existing is not null && existing.ExpirationUtc > renewBeforeUtc)
            {
                skipped++;
                continue;
            }

            var result = await _createSubscription.HandleAsync(new CreateCalendarSubscriptionRequest
            {
                UserId = mailboxUserId!
            }, ct);

            if (result.IsSuccess)
            {
                createdOrRenewed++;
                continue;
            }

            failures++;
            _logger.LogWarning(
                "Failed to create/renew calendar subscription for adviser mailbox {MailboxUserId}. Status={StatusCode} Error={ErrorCode} Message={ErrorMessage}",
                mailboxUserId,
                (int)result.StatusCode,
                result.ErrorCode,
                result.ErrorMessage);
        }

        await _syncState.UpsertValueAsync(SyncCursorKey, now.ToString("O"), now, ct);
        return new AdviserDirectorySyncResult
        {
            SyncedAtUtc = now,
            SyncedCount = records.Count,
            MailboxesDetected = mailboxUserIds.Count,
            SubscriptionsCreatedOrRenewed = createdOrRenewed,
            SubscriptionsSkipped = skipped,
            SubscriptionFailures = failures,
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
        public string? UserId { get; set; }
        public string? Mailbox { get; set; }
        public string? Email { get; set; }
        public string? AdviserEmail { get; set; }
        public string? PrincipalName { get; set; }
        public string? Region { get; set; }
        public string? Postcode { get; set; }
        public int MaxTravelTimeMinutes { get; set; }
        public double RadiusMiles { get; set; }
    }

    private string? ResolveMailboxUserId(CoverageAdviserItem adviser)
    {
        var candidates = new[]
        {
            adviser.UserId,
            adviser.Mailbox,
            adviser.Email,
            adviser.AdviserEmail,
            adviser.PrincipalName,
            adviser.Id
        };

        foreach (var raw in candidates)
        {
            var value = raw?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (value.Contains('@'))
                return value;

            if (_options.AllowNonEmailMailboxIds)
                return value;
        }

        return null;
    }
}
