using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Integration;

public sealed class HttpNotificationPublisher : INotificationPublisher
{
    private readonly HttpClient _httpClient;
    private readonly HttpNotificationPublisherOptions _options;

    public HttpNotificationPublisher(HttpClient httpClient, IOptions<HttpNotificationPublisherOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{HttpNotificationPublisherOptions.SectionName}:BaseUrl is required when Notifications:Integration:Transport is Http.");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.RequestPath);
        request.Content = JsonContent.Create(notification);

        if (!string.IsNullOrWhiteSpace(notification.CorrelationId))
            request.Headers.TryAddWithoutValidation("x-correlation-id", notification.CorrelationId);

        if (notification.Data.TryGetValue("IdempotencyKey", out var idempotencyKey) && !string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.Trim());

        if (!string.IsNullOrWhiteSpace(_options.InternalToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.InternalToken.Trim());

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
