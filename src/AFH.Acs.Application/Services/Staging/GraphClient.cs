using AFH.Acs.Recorder.Models;
using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Services;

public class GraphClient : IGraphClient
{
    private readonly IGraphTokenProvider _tokenProvider;
    private readonly HttpClient _http;
    private readonly GraphSettings _settings;

    public GraphClient(IGraphTokenProvider tokenProvider, HttpClient http, GraphSettings? settings = null)
    {
        _tokenProvider = tokenProvider;
        _http = http;
        _settings = settings ?? new GraphSettings();
    }

    public async Task<string> GetUsersAsync()
    {
        var token = await _tokenProvider.GetTokenAsync();
        var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/users");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    public async Task<string> GetCalendarAsync(string start, string end, string calendarUser)
    {
        var token = await _tokenProvider.GetTokenAsync();
        var url = $"https://graph.microsoft.com/v1.0/users/{calendarUser}/calendar/calendarView?startDateTime={start}&endDateTime={end}&$orderby=start/dateTime";

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _http.SendAsync(req);
        //resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }


    public async Task<string> CreateAdviserMeetingAsync(
        GraphMeetingCreateRequest request,
        CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetTokenAsync();

        // We’ll create the event in the “CalendarUser” mailbox
        // (this can be the adviser directly, or a shared mailbox if you prefer)
        var userId = request.AdviserEmail;
        var url = $"https://graph.microsoft.com/v1.0/users/{userId}/events";

        var bodyHtml =
            request.BodyHtml ??
            $@"<p>Dear {request.ClientName},</p>
                <p>Your AFH adviser {request.AdviserName} has scheduled a meeting with you.</p>
                <p><strong>Join link:</strong> <a href=""{request.JoinUrl}"">{request.JoinUrl}</a></p>
                <p>Kind regards,<br/>AFH Wealth Management</p>";

        var payload = new
        {
            subject = request.Subject,
            body = new
            {
                contentType = "HTML",
                content = bodyHtml
            },
            start = new
            {
                dateTime = request.Start.ToString("o"),
                timeZone = request.TimeZone
            },
            end = new
            {
                dateTime = request.End.ToString("o"),
                timeZone = request.TimeZone
            },
            location = new
            {
                displayName = request.Location ?? "Online – AFH Meeting"
            },
            attendees = new[]
            {
                new
                {
                    emailAddress = new { address = request.AdviserEmail, name = request.AdviserName },
                    type = "required"
                },
                new
                {
                    emailAddress = new { address = request.ClientEmail, name = request.ClientName },
                    type = "required"
                }
            }
            // NOTE: We’re not using Teams onlineMeeting here; the ACS join URL is in the body.
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _http.SendAsync(httpReq, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var eventId = doc.RootElement.GetProperty("id").GetString();

        return eventId ?? string.Empty;
    }


    public async Task<string> CreateEventAsync(
     string subject,
     string bodyHtml,
     DateTimeOffset start,
     DateTimeOffset end,
     IEnumerable<string> attendeesEmails,
     string? timeZone = "UTC")
    {
        var token = await _tokenProvider.GetTokenAsync();

        var evt = new
        {
            subject,
            body = new
            {
                contentType = "HTML",
                content = bodyHtml
            },
            start = new
            {
                dateTime = start.ToString("o"),
                timeZone
            },
            end = new
            {
                dateTime = end.ToString("o"),
                timeZone
            },
            location = new
            {
                displayName = "Online meeting (ACS)"
            },
            attendees = attendeesEmails.Select(a => new
            {
                emailAddress = new { address = a },
                type = "required"
            })
        };

        var json = JsonSerializer.Serialize(evt);
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{_settings.CalendarUser}/events")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respJson);
        var eventId = doc.RootElement.GetProperty("id").GetString();

        return eventId!;
    }
}
