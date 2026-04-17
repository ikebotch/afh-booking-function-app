using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AFH.Booking.Infrastructure.Meetings;

public sealed class AcsMeetingLinkFactory : IMeetingLinkFactory
{
    private readonly IOptions<AcsOptions> _opts;
    private readonly ILogger<AcsMeetingLinkFactory> _logger;
    private readonly HttpClient _http;
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _tx;
    private readonly IClientDirectory _clients;

    public AcsMeetingLinkFactory(
        HttpClient http,
        IOptions<AcsOptions> opts,
        ILogger<AcsMeetingLinkFactory> logger,
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository tx,
        IClientDirectory clients)
    {
        _http = http;
        _opts = opts;
        _logger = logger;
        _holds = holds;
        _slots = slots;
        _tx = tx;
        _clients = clients;
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

        if (string.IsNullOrWhiteSpace(bookingId))
            return null;

        // Load booking context needed by the ACS meet/create endpoint.
        // Important: keep these sequential (scoped EF DbContext is not concurrency-safe).
        var hold = await _holds.GetAsync(bookingId.Trim(), ct);
        if (hold is null || string.IsNullOrWhiteSpace(hold.SlotId))
            return null;

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return null;

        var tx = await _tx.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return null;

        var client = await _clients.GetAsync(tx.TransactionRef, ct);
        var clientEmail = string.IsNullOrWhiteSpace(client?.Email) ? null : client!.Email!.Trim();
        if (string.IsNullOrWhiteSpace(clientEmail))
        {
            _logger.LogWarning("ACS enabled but client email not available for booking. BookingId={BookingId} TransactionRef={TransactionRef}", bookingId, tx.TransactionRef);
            return null;
        }

        var meetingType = string.IsNullOrWhiteSpace(tx.MeetingType) ? "Review" : tx.MeetingType.Trim();
        var title = string.IsNullOrWhiteSpace(tx.MeetingType) ? "AFH Booking" : $"AFH Booking - {tx.MeetingType.Trim()}";

        var requestBody = new ScheduleMeetingRequest
        {
            AdviserId = slot.AdviserId,
            LeadId = string.IsNullOrWhiteSpace(client?.PartnerLeadId) ? tx.TransactionRef : client!.PartnerLeadId!.Trim(),
            MeetingType = meetingType,
            Title = title,
            Description = null,
            Start = new DateTimeOffset(slot.StartUtc, TimeSpan.Zero),
            End = new DateTimeOffset(slot.EndUtc, TimeSpan.Zero),
            ClientEmail = clientEmail,
            ClientName = BuildClientName(client),
            Location = null
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/meet/create")
        {
            Content = JsonContent.Create(requestBody)
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

        var payload = await response.Content.ReadFromJsonAsync<ScheduleMeetingResponse>(cancellationToken: ct);

        // Booking confirmation response is client-facing. Preserve existing behavior by returning the client URL.
        return string.IsNullOrWhiteSpace(payload?.ClientJoinUrl) ? null : payload!.ClientJoinUrl;
    }

    private static string? BuildClientName(Domain.Client.ClientDirectoryItem? client)
    {
        if (client is null) return null;

        var first = string.IsNullOrWhiteSpace(client.FirstName) ? null : client.FirstName.Trim();
        var last = string.IsNullOrWhiteSpace(client.LastName) ? null : client.LastName.Trim();

        var value = string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed class ScheduleMeetingRequest
    {
        public string AdviserId { get; init; } = string.Empty;
        public string LeadId { get; init; } = string.Empty;
        public string MeetingType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateTimeOffset Start { get; init; }
        public DateTimeOffset End { get; init; }
        public string ClientEmail { get; init; } = string.Empty;
        public string? ClientName { get; init; }
        public string? Location { get; init; }
    }

    private sealed class ScheduleMeetingResponse
    {
        [JsonPropertyName("clientJoinUrl")]
        public string? ClientJoinUrl { get; init; }

        [JsonPropertyName("adviserJoinUrl")]
        public string? AdviserJoinUrl { get; init; }
    }
}
