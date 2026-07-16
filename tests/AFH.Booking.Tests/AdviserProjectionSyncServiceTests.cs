using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Infrastructure.Http;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class AdviserProjectionSyncServiceTests
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
                            "skills": ["Equity Release", "Protection", " protection "],
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
        var meetingTopics = new RecordingMeetingTopics();
        var syncState = new RecordingSyncState();
        var sut = new AdviserProjectionSyncService(
            new HttpClient(handler),
            Options.Create(new AdviserDirectoryOptions
            {
                Enabled = true,
                BaseUrl = "https://location.example",
                CoverageEndpointPath = "/api/v1/admin/adviser-coverage",
                FunctionKey = "location-function-key",
                InternalToken = "location-token"
            }),
            new InternalBearerServiceAuthenticator(),
            profiles,
            meetingTopics,
            syncState,
            new StubClock(now),
            new RecordingLogSink(),
            Options.Create(new ApplicationLoggingOptions()),
            NullLogger<AdviserProjectionSyncService>.Instance);

        var result = await sut.SyncAsync(CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("https://location.example/api/v1/admin/adviser-coverage", captured!.RequestUri!.ToString());
        Assert.True(captured.Headers.TryGetValues("x-functions-key", out var functionKeyValues));
        Assert.Equal("location-function-key", Assert.Single(functionKeyValues));
        Assert.Equal("location-token", captured.Headers.Authorization?.Parameter);
        Assert.Equal(1, result.SyncedCount);
        Assert.Equal(2, result.DiscoveredMeetingTopicCount);

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

        Assert.Collection(
            meetingTopics.Upserts,
            topic =>
            {
                Assert.Equal("Equity Release", topic.Code);
                Assert.Equal("Equity Release", topic.Label);
                Assert.False(topic.IsDefault);
                Assert.True(topic.IsActive);
                Assert.Equal(now, topic.ChangedUtc);
            },
            topic =>
            {
                Assert.Equal("Protection", topic.Code);
                Assert.Equal("Protection", topic.Label);
                Assert.False(topic.IsDefault);
                Assert.True(topic.IsActive);
                Assert.Equal(now, topic.ChangedUtc);
            });

        Assert.Equal("adviser_directory_last_sync_utc", syncState.LastKey);
        Assert.Equal(now.ToString("O"), syncState.LastValue);
        Assert.Equal(now, syncState.LastUpdatedUtc);
    }

    [Fact]
    public async Task SyncAsync_WritesFailureToApplicationLogSink_WhenLocationReturnsUnauthorized()
    {
        var now = new DateTime(2026, 04, 02, 12, 30, 0, DateTimeKind.Utc);
        var logSink = new RecordingLogSink();
        var sut = new AdviserProjectionSyncService(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))),
            Options.Create(new AdviserDirectoryOptions
            {
                Enabled = true,
                BaseUrl = "https://location.example",
                CoverageEndpointPath = "/api/v1/admin/adviser-coverage",
                FunctionKey = "location-function-key",
                InternalToken = "location-token"
            }),
            new InternalBearerServiceAuthenticator(),
            new RecordingProfiles(),
            new RecordingMeetingTopics(),
            new RecordingSyncState(),
            new StubClock(now),
            logSink,
            Options.Create(new ApplicationLoggingOptions()),
            NullLogger<AdviserProjectionSyncService>.Instance);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.SyncAsync(CancellationToken.None));

        Assert.Contains("401", ex.Message, StringComparison.OrdinalIgnoreCase);
        var entry = Assert.Single(logSink.Entries);
        Assert.Equal("AdviserProjectionSync", entry.Category);
        Assert.Equal("AdviserProjectionDeltaSync", entry.Operation);
        Assert.Equal("AdviserProjectionSyncFailed", entry.EventType);
        Assert.Equal("Failure", entry.Result);
        Assert.Equal("Warning", entry.Level);
        Assert.Equal("HttpRequestException", entry.ExceptionType);
        Assert.Contains("\"StatusCode\":401", entry.PayloadJson);
        Assert.Contains("/api/v1/admin/adviser-coverage", entry.PayloadJson);
        Assert.DoesNotContain("location-token", entry.PayloadJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("location-function-key", entry.PayloadJson ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SyncAsync_DoesNotDuplicateAlreadyConfiguredMeetingTopics()
    {
        var now = new DateTime(2026, 04, 02, 13, 0, 0, DateTimeKind.Utc);
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": {
                        "advisers": [
                          {
                            "id": "adv-1",
                            "name": "Adviser One",
                            "skills": ["Protection", "Equity Release"]
                          }
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        var meetingTopics = new RecordingMeetingTopics(
        [
            new MeetingTopicRecord
            {
                Code = "Protection",
                Label = "Protection",
                IsDefault = true,
                SortOrder = 1
            }
        ]);

        var sut = new AdviserProjectionSyncService(
            new HttpClient(handler),
            Options.Create(new AdviserDirectoryOptions
            {
                Enabled = true,
                BaseUrl = "https://location.example",
                CoverageEndpointPath = "/api/v1/admin/adviser-coverage"
            }),
            new InternalBearerServiceAuthenticator(),
            new RecordingProfiles(),
            meetingTopics,
            new RecordingSyncState(),
            new StubClock(now),
            new RecordingLogSink(),
            Options.Create(new ApplicationLoggingOptions()),
            NullLogger<AdviserProjectionSyncService>.Instance);

        var result = await sut.SyncAsync(CancellationToken.None);

        Assert.Equal(1, result.DiscoveredMeetingTopicCount);
        var topic = Assert.Single(meetingTopics.Upserts);
        Assert.Equal("Equity Release", topic.Code);
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

    private sealed class RecordingMeetingTopics(IReadOnlyList<MeetingTopicRecord>? existingTopics = null) : IMeetingTopicRepository
    {
        public List<MeetingTopicUpsert> Upserts { get; } = [];

        public Task<IReadOnlyList<MeetingTopicRecord>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult(existingTopics ?? []);

        public Task<MeetingTopicRecord> UpsertAsync(MeetingTopicUpsert change, CancellationToken ct)
        {
            Upserts.Add(change);
            return Task.FromResult(new MeetingTopicRecord
            {
                Code = change.Code,
                Label = change.Label,
                IsDefault = change.IsDefault,
                SortOrder = change.SortOrder
            });
        }

        public Task<bool> DeactivateAsync(string code, DateTime changedUtc, CancellationToken ct)
            => Task.FromResult(false);
    }

    private sealed class RecordingLogSink : IApplicationLogSink
    {
        public List<ApplicationLogEntry> Entries { get; } = [];

        public Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
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
