using AFH.Booking.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace AFH.Booking.Infrastructure.Calendar;

public sealed class CalendarService : ICalendarService
{
    private readonly HttpClient _http;
    private readonly CalendarServiceOptions _options;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        IHttpClientFactory httpFactory,
        IOptions<CalendarServiceOptions> options,
        ILogger<CalendarService> logger)
    {
        _http = httpFactory.CreateClient(nameof(CalendarService));
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateEventAsync(BookingsModel booking, CancellationToken ct)
    {
        EnsureBaseUrl();

        var payload = new
        {
            userId = booking.AdviserId,
            bookingId = booking.Id.Value,
            subject = string.IsNullOrWhiteSpace(booking.Subject) ? "AFH Booking" : booking.Subject,
            startUtc = booking.StartUtc,
            endUtc = booking.EndUtc,
            timezone = string.IsNullOrWhiteSpace(booking.Timezone) ? "UTC" : booking.Timezone,
            body = booking.Notes
        };

        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/appointments";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        AddAuth(req);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<CreateAppointmentResponse>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(body?.AppointmentId))
            throw new InvalidOperationException("Calendar service did not return an appointment id.");

        return body.AppointmentId;
    }

    public async Task CancelEventAsync(string userId, string providerEventId, CancellationToken ct)
    {
        EnsureBaseUrl();

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/appointments/{Uri.EscapeDataString(providerEventId)}" +
            $"?userId={Uri.EscapeDataString(userId)}";

        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        AddAuth(req);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CalendarScheduleItem>> GetScheduleAsync(
        string userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("CalendarService:BaseUrl is missing; returning empty schedule for UserId={UserId}", userId);
            return Array.Empty<CalendarScheduleItem>();
        }

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/calendar/users/{Uri.EscapeDataString(userId)}/schedule" +
            $"?startUtc={Uri.EscapeDataString(startUtc.ToString("O"))}" +
            $"&endUtc={Uri.EscapeDataString(endUtc.ToString("O"))}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuth(req);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Calendar schedule lookup failed for UserId={UserId}, StatusCode={StatusCode}",
                userId,
                (int)resp.StatusCode);
            return Array.Empty<CalendarScheduleItem>();
        }

        var body = await resp.Content.ReadFromJsonAsync<ScheduleResponse>(cancellationToken: ct);
        if (body?.Bookings is null || body.Bookings.Count == 0)
            return Array.Empty<CalendarScheduleItem>();

        return body.Bookings.Select(x => new CalendarScheduleItem
        {
            BookingId = x.BookingId,
            Subject = x.Subject,
            StartUtc = x.StartUtc,
            EndUtc = x.EndUtc,
            Status = x.Status
        }).ToList();
    }

    private void EnsureBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("CalendarService:BaseUrl is required.");
    }

    private void AddAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            request.Headers.Add("x-functions-key", _options.FunctionKey);
    }

    private sealed class CreateAppointmentResponse
    {
        public string AppointmentId { get; set; } = string.Empty;
    }

    private sealed class ScheduleResponse
    {
        public List<BookingSummary> Bookings { get; set; } = new();
    }

    private sealed class BookingSummary
    {
        public string BookingId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
