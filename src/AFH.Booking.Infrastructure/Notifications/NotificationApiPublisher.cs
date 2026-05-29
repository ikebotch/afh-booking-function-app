using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Infrastructure.Notifications.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Notifications;

public sealed class NotificationApiPublisher : IBookingNotificationPublisher
{
    private readonly HttpClient _httpClient;
    private readonly NotificationApiPublisherOptions _options;
    private readonly ILogger<NotificationApiPublisher> _logger;

    public NotificationApiPublisher(
        HttpClient httpClient,
        IOptions<NotificationApiPublisherOptions> options,
        ILogger<NotificationApiPublisher> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(BookingNotificationRequest notification, CancellationToken ct)
    {
        var baseUrl = Require(_options.BaseUrl, $"{NotificationApiPublisherOptions.SectionName}:BaseUrl");
        var functionKey = Require(_options.FunctionKey, $"{NotificationApiPublisherOptions.SectionName}:FunctionKey");
        var internalToken = Require(_options.InternalToken, $"{NotificationApiPublisherOptions.SectionName}:InternalToken");

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(baseUrl, _options.RequestPath, functionKey));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", internalToken);
        request.Content = JsonContent.Create(notification);

        if (!string.IsNullOrWhiteSpace(notification.CorrelationId))
            request.Headers.TryAddWithoutValidation("x-correlation-id", notification.CorrelationId);

        if (notification.Data.TryGetValue("IdempotencyKey", out var idempotencyKey) && !string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.Trim());

        _logger.LogInformation(
            "Notification API publish started. NotificationType={NotificationType} RequestPath={RequestPath}",
            notification.Type.Name,
            _options.RequestPath);

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation(
            "Notification API publish response received. NotificationType={NotificationType} StatusCode={StatusCode} ResponseBody={ResponseBody}",
            notification.Type.Name,
            (int)response.StatusCode,
            responseBody);

        response.EnsureSuccessStatusCode();
    }

    private static Uri BuildRequestUri(string baseUrl, string requestPath, string functionKey)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var relativePath = string.IsNullOrWhiteSpace(requestPath)
            ? "/api/v1/notifications/requests"
            : requestPath.TrimStart('/');
        var builder = new UriBuilder(new Uri(baseUri, relativePath));
        var existingQuery = builder.Query.TrimStart('?');
        var codeQuery = $"code={Uri.EscapeDataString(functionKey)}";
        builder.Query = string.IsNullOrWhiteSpace(existingQuery)
            ? codeQuery
            : $"{existingQuery}&{codeQuery}";
        return builder.Uri;
    }

    private static string Require(string? value, string key)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        throw new InvalidOperationException($"{key} is required for booking notification HTTP publishing.");
    }
}
