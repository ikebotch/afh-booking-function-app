using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Infrastructure.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class AdviserDirectorySyncServiceTests
{
    [Fact]
    public async Task SyncAsync_StoresAuthoritativeFieldsFromLocationCoverage()
    {
        var now = new DateTime(2026, 04, 02, 12, 0, 0, DateTimeKind.Utc);
        HttpRequestMessage? captured = null;

        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": {
                        "advisers": [
                          {
                            "id": "adv-1",
                            "name": "Adviser One",
                            "mailboxUserId": "adviser.one@tenant.com",
                            "region": "North",
                            "postcode": "AB1 2CD",
                            "isActive": false,
                            "skills": ["Equity Release", "Protection"],
                            "rating": 4.7,
                            "maxTravelTimeMinutes": 45,
                            "radiusMiles": 12.5
                          }
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var profiles = new RecordingProfiles();
        var syncState = new RecordingSyncState();
        var sut = new AdviserDirectorySyncService(
            new HttpClient(handler),
            Options.Create(new AdviserDirectoryOptions
            {
                Enabled = true,
                BaseUrl = "https://location.example",
                CoverageEndpointPath = "/api/v1/admin/adviser-coverage",
                InternalToken = "location-token"
            }),
            new InternalBearerServiceAuthenticator(),
            profiles,
            syncState,
            new StubClock(now));

        var result = await sut.SyncAsync(CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("https://location.example/api/v1/admin/adviser-coverage", captured!.RequestUri!.ToString());
        Assert.Equal("location-token", captured.Headers.Authorization?.Parameter);
        Assert.Equal(1, result.SyncedCount);

        var stored = Assert.Single(profiles.Upserts);
        Assert.Equal("adv-1", stored.AdviserId);
        Assert.Equal("Adviser One", stored.DisplayName);
        Assert.Equal("adviser.one@tenant.com", stored.MailboxUserId);
        Assert.Equal("North", stored.Region);
        Assert.Equal("AB1 2CD", stored.HomePostcode);
        Assert.False(stored.IsActive);
        Assert.Equal(4.7, stored.Rating);
        Assert.Equal(new[] { "Equity Release", "Protection" }, stored.Skills);
        Assert.Equal(45, stored.MaxTravelTimeMinutes);
        Assert.Equal(12.5, stored.CoverageRadiusMiles);
        Assert.Equal(now, stored.LastSyncedUtc);

        Assert.Equal("adviser_directory_last_sync_utc", syncState.LastKey);
        Assert.Equal(now.ToString("O"), syncState.LastValue);
        Assert.Equal(now, syncState.LastUpdatedUtc);
    }

    private sealed class RecordingProfiles : IAdviserProfileProjectionRepository
    {
        public List<AdviserProfileProjectionRecord> Upserts { get; } = [];

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct)
        {
            Upserts.AddRange(advisers);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>(Upserts);

        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>(Upserts.Where(x => x.IsActive).ToList());

        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
            => Task.FromResult<AdviserProfileProjectionRecord?>(Upserts.FirstOrDefault(x => string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class RecordingSyncState : IIntegrationSyncStateRepository
    {
        public string? LastKey { get; private set; }
        public string? LastValue { get; private set; }
        public DateTime LastUpdatedUtc { get; private set; }

        public Task<string?> GetValueAsync(string key, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task UpsertValueAsync(string key, string value, DateTime updatedUtc, CancellationToken ct)
        {
            LastKey = key;
            LastValue = value;
            LastUpdatedUtc = updatedUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
