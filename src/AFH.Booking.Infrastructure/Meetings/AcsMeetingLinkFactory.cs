using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace AFH.Booking.Infrastructure.Meetings;

public sealed class AcsMeetingLinkFactory : IMeetingLinkFactory
{
    private readonly IOptions<AcsOptions> _opts;
    private readonly ILogger<AcsMeetingLinkFactory> _logger;
    private readonly HttpClient _http;

    public AcsMeetingLinkFactory(
        HttpClient http,
        IOptions<AcsOptions> opts,
        ILogger<AcsMeetingLinkFactory> logger)
    {
        _http = http;
        _opts = opts;
        _logger = logger;
    }

    public async Task<string?> CreateJoinLinkAsync(string bookingId, CancellationToken ct)
    {
        if (!_opts.Value.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(_opts.Value.MeetingLinkServiceBaseUrl))
        {
            _logger.LogWarning("ACS enabled but MeetingLinkServiceBaseUrl not configured.");
            return null;
        }

        // Example call; align to your actual endpoint
        var res = await _http.PostAsJsonAsync("/api/v1/meetings/link", new { bookingId }, ct);
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<MeetingLinkResponse>(cancellationToken: ct);
        return payload?.JoinUrl;
    }

    private sealed class MeetingLinkResponse
    {
        public string? JoinUrl { get; set; }
    }
}
