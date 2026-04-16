using System.Net;
using System.Net.Http;
using System.Text;
using AFH.Acs.Infrastructure.Advisers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Tests;

public sealed class LocationAdviserInfoProviderTests
{
    [Fact]
    public async Task GetByIdAsync_Maps_And_Caches_Location_Adviser_Info()
    {
        var handler = new StubHttpMessageHandler("""
            {
              "advisers": [
                {
                  "id": "adv-123",
                  "name": "Adviser Example",
                  "mailboxUserId": "mailbox-123"
                }
              ]
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://location.example")
        };

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new LocationAdviserInfoProvider(
            httpClient,
            memoryCache,
            Options.Create(new LocationAdviserInfoOptions
            {
                BaseUrl = "https://location.example",
                CoveragePath = "/api/v1/admin/adviser-coverage",
                CacheDuration = TimeSpan.FromMinutes(5)
            }),
            NullLogger<LocationAdviserInfoProvider>.Instance);

        var first = await provider.GetByIdAsync("adv-123");
        var second = await provider.GetByIdAsync("adv-123");

        Assert.NotNull(first);
        Assert.Equal("adv-123", first!.AdviserId);
        Assert.Equal("Adviser Example", first.DisplayName);
        Assert.Equal("mailbox-123", first.MailboxUserId);
        Assert.NotNull(second);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
