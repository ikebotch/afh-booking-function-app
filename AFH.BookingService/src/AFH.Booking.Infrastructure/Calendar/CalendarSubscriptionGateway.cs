using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Calendar;

public sealed class CalendarSubscriptionGateway : ICalendarSubscriptionGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly CalendarSubscriptionOptions _opts;
    private readonly ILogger<CalendarSubscriptionGateway> _logger;

    public CalendarSubscriptionGateway(
        HttpClient http,
        IOptions<CalendarSubscriptionOptions> opts,
        ILogger<CalendarSubscriptionGateway> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<CreateCalendarSubscriptionResult> CreateOrRenewAsync(
        CreateCalendarSubscriptionRequest request,
        CancellationToken ct)
    {
        var result = await CreateAsync(request, ct);
        if (!result.IsSuccess || result.Value is null)
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to create calendar subscription.");

        return result.Value;
    }

    public async Task<Result<CreateCalendarSubscriptionResult>> CreateAsync(
        CreateCalendarSubscriptionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<CreateCalendarSubscriptionResult>.Fail(HttpStatusCode.BadRequest, "AdviserUserId is required.", "Validation");

        var payload = new
        {
            userId = request.UserId,
            notificationUrl = request.NotificationUrl,
            clientState = request.ClientState,
            resource = request.Resource,
            expirationUtc = request.ExpirationUtc
        };

        var url = BuildUrl("/api/v1/calendar/subscriptions", includeFunctionKeyInQuery: true);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Calendar subscription create failed. Status={StatusCode} Body={Body}",
                (int)res.StatusCode,
                body);

            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadGateway,
                "Failed to create calendar subscription via calendar service.",
                "CalendarServiceError");
        }

        var created = await res.Content.ReadFromJsonAsync<CreateCalendarSubscriptionResponse>(JsonOptions, ct);
        if (created is null || string.IsNullOrWhiteSpace(created.SubscriptionId))
        {
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadGateway,
                "Calendar service returned an invalid subscription response.",
                "CalendarServiceError");
        }

        return Result<CreateCalendarSubscriptionResult>.Ok(new CreateCalendarSubscriptionResult
        {
            SubscriptionId = created.SubscriptionId,
            ExpirationUtc = created.ExpirationUtc,
            Resource = created.Resource
        });
    }

    public async Task<Result> DeleteAsync(string subscriptionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Fail(HttpStatusCode.BadRequest, "subscriptionId is required.", "Validation");

        var url = BuildUrl($"/api/v1/calendar/subscriptions/{Uri.EscapeDataString(subscriptionId)}", includeFunctionKeyInQuery: true);
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        AddAuth(req);

        using var res = await _http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound)
            return Result.NotFound("Subscription not found.");

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Calendar subscription delete failed. Status={StatusCode} Body={Body}",
                (int)res.StatusCode,
                body);

            return Result.Fail(
                HttpStatusCode.BadGateway,
                "Failed to delete calendar subscription via calendar service.",
                "CalendarServiceError");
        }

        return Result.Ok();
    }

    private string BuildUrl(string path, bool includeFunctionKeyInQuery)
    {
        var baseUrl = _opts.BaseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";

        if (!includeFunctionKeyInQuery || string.IsNullOrWhiteSpace(_opts.FunctionKey))
            return baseUrl + normalizedPath;

        var separator = normalizedPath.Contains('?') ? "&" : "?";
        return $"{baseUrl}{normalizedPath}{separator}code={Uri.EscapeDataString(_opts.FunctionKey)}";
    }

    private void AddAuth(HttpRequestMessage req)
    {
        if (!string.IsNullOrWhiteSpace(_opts.FunctionKey))
            req.Headers.TryAddWithoutValidation("x-functions-key", _opts.FunctionKey);
    }

    private sealed class CreateCalendarSubscriptionResponse
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public DateTimeOffset ExpirationUtc { get; set; }
        public string? Resource { get; set; }
    }
}
