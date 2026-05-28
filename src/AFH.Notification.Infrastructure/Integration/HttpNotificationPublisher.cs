using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Integration;

public sealed class HttpNotificationPublisher : INotificationPublisher
{
    private readonly HttpClient _httpClient;
    private readonly HttpNotificationPublisherOptions _options;
    private readonly ILogger<HttpNotificationPublisher> _logger;

    public HttpNotificationPublisher(
        HttpClient httpClient,
        IOptions<HttpNotificationPublisherOptions> options,
        ILogger<HttpNotificationPublisher>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger ?? NullLogger<HttpNotificationPublisher>.Instance;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{HttpNotificationPublisherOptions.SectionName}:BaseUrl is required when Notifications:Integration:Transport is Http.");

        _logger.LogInformation(
            "Selected publisher transport. PublisherTransport=Http RequestPath={RequestPath}",
            _options.RequestPath);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.RequestPath);
        request.Content = JsonContent.Create(notification);

        if (!string.IsNullOrWhiteSpace(notification.CorrelationId))
            request.Headers.TryAddWithoutValidation("x-correlation-id", notification.CorrelationId);

        if (notification.Data.TryGetValue("IdempotencyKey", out var idempotencyKey) && !string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.Trim());

        var internalToken = ResolveInternalToken();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", internalToken);

        _logger.LogInformation(
            "Notification HTTP publish started. NotificationType={NotificationType} RequestPath={RequestPath}",
            notification.Type.Name,
            _options.RequestPath);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Notification HTTP publish succeeded. NotificationType={NotificationType} StatusCode={StatusCode}",
                notification.Type.Name,
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notification HTTP publish failed. NotificationType={NotificationType} RequestPath={RequestPath}",
                notification.Type.Name,
                _options.RequestPath);
            throw;
        }
    }

    private string ResolveInternalToken()
    {
        if (!string.IsNullOrWhiteSpace(_options.InternalToken))
            return _options.InternalToken.Trim();

        throw new InvalidOperationException(
            "Notifications:Integration:Http:InternalToken is required for HTTP notification publishing.");
    }
}
