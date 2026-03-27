using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/meetings/link")
        {
            Content = JsonContent.Create(new { bookingId })
        };

        if (!string.IsNullOrWhiteSpace(_opts.Value.FunctionKey))
            request.Headers.TryAddWithoutValidation("x-functions-key", _opts.Value.FunctionKey.Trim());

        if (!string.IsNullOrWhiteSpace(_opts.Value.InternalToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.Value.InternalToken.Trim());

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ACS meeting-link request failed. BookingId={BookingId} Status={Status} FailureCategory={FailureCategory}",
                bookingId,
                (int)response.StatusCode,
                DownstreamFailureClassifier.Classify(response.StatusCode));
            throw new HttpRequestException("ACS meeting link request failed.", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<MeetingLinkResponse>(cancellationToken: ct);
        return payload?.JoinUrl;
    }

    private sealed class MeetingLinkResponse
    {
        public string? JoinUrl { get; set; }
    }
}
