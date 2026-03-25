using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Infrastructure.Http;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Calendar;

public sealed class CalendarGateway : ICalendarGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly CalendarSubscriptionOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly ILogger<CalendarGateway> _logger;

    public CalendarGateway(
        HttpClient http,
        IOptions<CalendarSubscriptionOptions> options,
        IInternalServiceAuthenticator authenticator,
        ILogger<CalendarGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
    {
        EnsureConfigured();

        var payload = new
        {
            userId = ev.UserId,
            bookingId = ev.ExternalId,
            subject = ev.Subject,
            startUtc = ev.StartUtc,
            endUtc = ev.EndUtc,
            timezone = ev.Timezone,
            isRemote = ev.IsRemote,
            categories = ev.Categories,
            showAs = ev.ShowAs.ToString(),
            body = ev.Body,
            location = ev.Location is null
                ? null
                : new
                {
                    displayName = ev.Location.DisplayName,
                    addressLine1 = ev.Location.AddressLine1,
                    city = ev.Location.City,
                    postcode = ev.Location.Postcode
                },
            attendees = ev.Attendees.Select(a => new
            {
                email = a.Email,
                name = a.Name,
                isRequired = a.IsRequired
            })
        };

        var url = BuildUrl("/api/v1/calendar/appointments");
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var created = await ReadEnvelopedOrRawAsync<CreateAppointmentResponse>(res, ct);
        return created?.AppointmentId ?? created?.EventId;
    }

    public async Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(ev.EventId))
            return null;

        var payload = new
        {
            userId = ev.UserId,
            body = ev.Body,
            categories = ev.Categories,
            showAs = ev.ShowAs.ToString()
        };

        var url = BuildUrl($"/api/v1/calendar/appointments/{Uri.EscapeDataString(ev.EventId)}");
        using var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;

        res.EnsureSuccessStatusCode();

        var updated = await ReadEnvelopedOrRawAsync<CreateAppointmentResponse>(res, ct);
        return updated?.AppointmentId ?? updated?.EventId ?? ev.EventId;
    }

    public async Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct)
    {
        EnsureConfigured();

        var path =
            $"/api/v1/calendar/appointments/{Uri.EscapeDataString(providerEventId)}?userId={Uri.EscapeDataString(userId)}";
        var url = BuildUrl(path);

        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return;

        res.EnsureSuccessStatusCode();
    }

    public async Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default)
    {
        EnsureConfigured();

        var path = $"/api/v1/calendar/appointments/{Uri.EscapeDataString(eventId)}?userId={Uri.EscapeDataString(userId)}";
        var url = BuildUrl(path);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Calendar event lookup failed for EventId={EventId}. Status={Status}", eventId, (int)res.StatusCode);
            return null;
        }

        var payload = await ReadEnvelopedOrRawAsync<CalendarEventResponse>(res, ct);
        if (payload is null)
            return null;

        return new CalendarEventDetails
        {
            CalendarId = payload.CalendarId ?? string.Empty,
            Subject = payload.Subject ?? string.Empty,
            StartUtc = payload.StartUtc,
            EndUtc = payload.EndUtc,
            ChangeKey = payload.ChangeKey,
            ICalUId = payload.ICalUId,
            ShowAs = payload.ShowAs,
            HasLocation = payload.HasLocation,
            LocationDisplayName = payload.LocationDisplayName,
            IsRecurring = payload.IsRecurring,
            RecurrencePattern = payload.RecurrencePattern
        };
    }

    public async Task<AdviserAvailabilityResult> CheckAvailabilityAsync(
        string userId,
        DateTime startUtc,
        DateTime endUtc,
        string timezone,
        CancellationToken ct)
    {
        EnsureConfigured();

        var path =
            $"/api/v1/calendar/users/{Uri.EscapeDataString(userId)}/schedule?startUtc={Uri.EscapeDataString(startUtc.ToString("O"))}&endUtc={Uri.EscapeDataString(endUtc.ToString("O"))}";
        var url = BuildUrl(path);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            return new AdviserAvailabilityResult
            {
                IsFree = false,
                MailboxUnavailable = true,
                StatusMessage = $"Calendar schedule lookup failed with status {(int)res.StatusCode}.",
                Conflicts = Array.Empty<CalendarConflictBlock>()
            };
        }

        var schedule = await ReadEnvelopedOrRawAsync<ScheduleResponse>(res, ct) ?? new ScheduleResponse();

        var conflicts = schedule.Bookings
            .Where(b => b.EndUtc > startUtc && b.StartUtc < endUtc)
            .Select(b => new CalendarConflictBlock
            {
                StartUtc = b.StartUtc,
                EndUtc = b.EndUtc,
                Subject = b.Subject
            })
            .OrderBy(c => c.StartUtc)
            .ToList();

        return new AdviserAvailabilityResult
        {
            IsFree = conflicts.Count == 0,
            MailboxUnavailable = false,
            StatusMessage = conflicts.Count == 0 ? "Free" : "Conflicts found",
            Conflicts = conflicts
        };
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("Calendar:BaseUrl is required.");
    }

    private string BuildUrl(string path)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return baseUrl + normalizedPath;
    }

    private void AddAuth(HttpRequestMessage req)
    {
        _authenticator.Apply(req, _options.InternalToken);
    }

    private static async Task<T?> ReadEnvelopedOrRawAsync<T>(HttpResponseMessage response, CancellationToken ct)
        where T : class
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            var enveloped = JsonSerializer.Deserialize<ApiEnvelope<T>>(json, JsonOptions);
            if (enveloped?.Data is not null)
                return enveloped.Data;

            // Backward compatibility for legacy non-enveloped responses.
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private sealed class CreateAppointmentResponse
    {
        public string? AppointmentId { get; set; }
        public string? EventId { get; set; }
    }

    private sealed class CalendarEventResponse
    {
        public string? CalendarId { get; set; }
        public string? Subject { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string? ChangeKey { get; set; }
        public string? ICalUId { get; set; }
        public string? ShowAs { get; set; }
        public bool HasLocation { get; set; }
        public string? LocationDisplayName { get; set; }
        public bool IsRecurring { get; set; }
        public string? RecurrencePattern { get; set; }
    }

    private sealed class ScheduleResponse
    {
        public List<ScheduleBooking> Bookings { get; set; } = new();
    }

    private sealed class ScheduleBooking
    {
        public string BookingId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
