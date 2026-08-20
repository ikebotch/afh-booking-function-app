using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Models.Clients;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Logging;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class DownstreamUpdateService : IDownstreamUpdateService, IDownstreamUpdateReconciliationService
{
    private readonly BookingDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PartnerWorkflowOptions _partnerWorkflowOptions;
    private readonly IPartnerWorkflowPolicyProvider _policyProvider;
    private readonly IApplicationLogSink _logSink;
    private readonly ApplicationLoggingOptions _loggingOptions;
    private readonly ILogger<DownstreamUpdateService> _logger;

    public DownstreamUpdateService(
        BookingDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<PartnerWorkflowOptions> partnerWorkflowOptions,
        IPartnerWorkflowPolicyProvider policyProvider,
        IApplicationLogSink logSink,
        IOptions<ApplicationLoggingOptions> loggingOptions,
        ILogger<DownstreamUpdateService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _partnerWorkflowOptions = partnerWorkflowOptions.Value;
        _policyProvider = policyProvider;
        _logSink = logSink;
        _loggingOptions = loggingOptions.Value;
        _logger = logger;
    }

    public async Task<DownstreamUpdateResponse> PublishBookingChangeAsync(
        string bookingId,
        string changeType,
        string transactionRef,
        string payloadJson,
        CancellationToken ct)
    {
        var row = new DownstreamUpdateModel
        {
            Id = Guid.NewGuid().ToString("N"),
            BookingId = bookingId,
            ChangeType = changeType,
            TransactionRef = transactionRef,
            PayloadJson = payloadJson,
            Status = "Pending",
            AttemptCount = 1,
            CreatedUtc = DateTime.UtcNow
        };

        _db.DownstreamUpdates.Add(row);
        await _db.SaveChangesAsync(ct);

        await DispatchAsync(row, correlationId: null, ct);

        return ToResponse(row);
    }

    public async Task<DownstreamUpdateReconciliationResponse> ReconcileAsync(
        int maxCount,
        int olderThanMinutes,
        bool includePending,
        string? correlationId,
        CancellationToken ct)
    {
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-Math.Max(0, olderThanMinutes));
        var candidates = (await _db.DownstreamUpdates
                .Where(x => (x.ProcessedUtc ?? x.CreatedUtc) <= cutoffUtc)
                .OrderBy(x => x.ProcessedUtc ?? x.CreatedUtc)
                .ToListAsync(ct))
            .Where(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                        (includePending && string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Clamp(maxCount, 1, 100))
            .ToList();

        var results = new List<DownstreamUpdateReconciliationItemResponse>(candidates.Count);
        foreach (var row in candidates)
        {
            var previousStatus = row.Status;
            row.AttemptCount += 1;
            await DispatchAsync(row, correlationId, ct);
            results.Add(new DownstreamUpdateReconciliationItemResponse
            {
                UpdateId = row.Id,
                BookingId = row.BookingId,
                ChangeType = row.ChangeType,
                PreviousStatus = previousStatus,
                CurrentStatus = row.Status,
                AttemptCount = row.AttemptCount,
                ProcessedUtc = row.ProcessedUtc,
                ErrorMessage = row.ErrorMessage
            });
        }

        return new DownstreamUpdateReconciliationResponse
        {
            RequestedCount = candidates.Count,
            RetriedCount = results.Count,
            SucceededCount = results.Count(x => string.Equals(x.CurrentStatus, "Sent", StringComparison.OrdinalIgnoreCase)),
            FailedCount = results.Count(x => !string.Equals(x.CurrentStatus, "Sent", StringComparison.OrdinalIgnoreCase)),
            Results = results
        };
    }

    private async Task DispatchAsync(
        DownstreamUpdateModel row,
        string? correlationId,
        CancellationToken ct)
    {
        var hasUpdateUri = TryResolveUpdateUri(out var updateUri);
        if (!_partnerWorkflowOptions.Enabled || !hasUpdateUri)
        {
            _logger.LogInformation(
                "Partner workflow booking change configured off. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} Enabled={Enabled} HasBookingUpdatesUrl={HasBookingUpdatesUrl} HasBaseUrl={HasBaseUrl} HasResolvedUpdateUri={HasResolvedUpdateUri} CorrelationId={CorrelationId}",
                row.Id,
                row.BookingId,
                row.ChangeType,
                _partnerWorkflowOptions.Enabled,
                !string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BookingUpdatesUrl),
                !string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BaseUrl),
                hasUpdateUri,
                correlationId);

            row.Status = "ConfiguredOff";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await TryWriteApplicationLogAsync(
                row,
                correlationId,
                level: "Information",
                eventType: "PartnerWorkflowConfiguredOff",
                result: "ConfiguredOff",
                message: "Partner workflow booking change configured off.",
                payload: new
                {
                    row.Id,
                    row.BookingId,
                    row.ChangeType,
                    row.TransactionRef,
                    _partnerWorkflowOptions.Enabled,
                    HasBookingUpdatesUrl = !string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BookingUpdatesUrl),
                    HasBaseUrl = !string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BaseUrl),
                    HasResolvedUpdateUri = hasUpdateUri
                },
                ct);
            return;
        }

        if (!await _policyProvider.IsEnabledAsync(row.ChangeType, ct))
        {
            row.Status = "Skipped";
            row.ErrorMessage = "PartnerWorkflow:ChangeTypeDisabled";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await TryWriteApplicationLogAsync(
                row,
                correlationId,
                level: "Information",
                eventType: "PartnerWorkflowSkipped",
                result: "Skipped",
                message: "Partner workflow booking change skipped by DB policy.",
                payload: new
                {
                    row.Id,
                    row.BookingId,
                    row.ChangeType,
                    row.TransactionRef,
                    row.ErrorMessage
                },
                ct);
            return;
        }

        try
        {
            _logger.LogInformation(
                "Publishing downstream booking change. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} AttemptCount={AttemptCount} CorrelationId={CorrelationId}",
                row.Id,
                row.BookingId,
                row.ChangeType,
                row.AttemptCount,
                correlationId);

            var http = _httpClientFactory.CreateClient("partner-workflow-updates");
            using var request = new HttpRequestMessage(HttpMethod.Post, updateUri);
            AddApiKeyHeader(request);
            AddIdempotencyHeader(request, row);
            request.Content = new StringContent(
                JsonSerializer.Serialize(BuildPayload(row)),
                Encoding.UTF8,
                "application/json");

            var response = await http.SendAsync(request, ct);
            row.Status = response.IsSuccessStatusCode ? "Sent" : "Failed";
            row.ErrorMessage = response.IsSuccessStatusCode
                ? null
                : $"PartnerWorkflow:{DownstreamFailureClassifier.Classify(response.StatusCode)}:{(int)response.StatusCode}";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await TryWriteApplicationLogAsync(
                row,
                correlationId,
                level: response.IsSuccessStatusCode ? "Information" : "Warning",
                eventType: response.IsSuccessStatusCode ? "PartnerWorkflowSent" : "PartnerWorkflowFailed",
                result: row.Status,
                message: response.IsSuccessStatusCode
                    ? "Partner workflow booking change sent."
                    : "Partner workflow booking change failed.",
                payload: new
                {
                    row.Id,
                    row.BookingId,
                    row.ChangeType,
                    row.TransactionRef,
                    StatusCode = (int)response.StatusCode,
                    FailureCategory = response.IsSuccessStatusCode ? null : DownstreamFailureClassifier.Classify(response.StatusCode),
                    row.ErrorMessage
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Partner workflow booking change failed. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} StatusCode={StatusCode} FailureCategory={FailureCategory} CorrelationId={CorrelationId}",
                    row.Id,
                    row.BookingId,
                    row.ChangeType,
                    (int)response.StatusCode,
                    DownstreamFailureClassifier.Classify(response.StatusCode),
                    correlationId);
            }
        }
        catch (Exception ex)
        {
            row.Status = "Failed";
            row.ErrorMessage = $"PartnerWorkflowException:{ex.GetType().Name}";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await TryWriteApplicationLogAsync(
                row,
                correlationId,
                level: "Warning",
                eventType: "PartnerWorkflowFailed",
                result: "Failure",
                message: "Partner workflow booking change threw.",
                payload: new
                {
                    row.Id,
                    row.BookingId,
                    row.ChangeType,
                    row.TransactionRef,
                    row.ErrorMessage
                },
                ct,
                exception: ex);

            _logger.LogWarning(
                ex,
                "Partner workflow booking change threw. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} AttemptCount={AttemptCount} CorrelationId={CorrelationId}",
                row.Id,
                row.BookingId,
                row.ChangeType,
                row.AttemptCount,
                correlationId);
        }
    }

    private async Task TryWriteApplicationLogAsync(
        DownstreamUpdateModel row,
        string? correlationId,
        string level,
        string eventType,
        string result,
        string message,
        object payload,
        CancellationToken ct,
        Exception? exception = null)
    {
        try
        {
            await _logSink.WriteAsync(new ApplicationLogEntry
            {
                OccurredUtc = DateTime.UtcNow,
                Level = level,
                Category = "PartnerWorkflow",
                Operation = "DownstreamBookingChange",
                CorrelationId = correlationId,
                ContextId = row.Id,
                EventType = eventType,
                Result = result,
                Message = message,
                ExceptionType = exception?.GetType().Name,
                ExceptionMessage = exception?.Message,
                PayloadJson = ApplicationLogPayloadHelper.Serialize(payload, _loggingOptions)
            }, ct);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(
                logEx,
                "Failed to persist partner workflow application log. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType}",
                row.Id,
                row.BookingId,
                row.ChangeType);
        }
    }

    private bool TryResolveUpdateUri(out Uri updateUri)
    {
        if (!string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BookingUpdatesUrl) &&
            Uri.TryCreate(_partnerWorkflowOptions.BookingUpdatesUrl.Trim(), UriKind.Absolute, out updateUri!))
        {
            return true;
        }

        updateUri = null!;
        if (string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BaseUrl))
            return false;

        var baseUri = _partnerWorkflowOptions.BaseUrl.TrimEnd('/') + "/";
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var parsedBaseUri))
            return false;

        var path = string.IsNullOrWhiteSpace(_partnerWorkflowOptions.BookingUpdatesPath)
            ? "/api/booking-updates"
            : _partnerWorkflowOptions.BookingUpdatesPath.Trim();

        updateUri = new Uri(parsedBaseUri, path.TrimStart('/'));
        return true;
    }

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_partnerWorkflowOptions.ApiKey))
            return;

        var headerName = string.IsNullOrWhiteSpace(_partnerWorkflowOptions.ApiKeyHeaderName)
            ? "Authorization"
            : _partnerWorkflowOptions.ApiKeyHeaderName.Trim();

        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _partnerWorkflowOptions.ApiKey.Trim());
            return;
        }

        request.Headers.TryAddWithoutValidation(headerName, _partnerWorkflowOptions.ApiKey.Trim());
    }

    private void AddIdempotencyHeader(HttpRequestMessage request, DownstreamUpdateModel row)
    {
        var headerName = string.IsNullOrWhiteSpace(_partnerWorkflowOptions.IdempotencyKeyHeaderName)
            ? "X-Idempotency-Key"
            : _partnerWorkflowOptions.IdempotencyKeyHeaderName.Trim();

        request.Headers.TryAddWithoutValidation(headerName, BuildIdempotencyKey(row));
    }

    private object BuildPayload(DownstreamUpdateModel row)
        => IsPartnerWorkflowPayload()
            ? BuildPartnerWorkflowPayload(row)
            : new
            {
                bookingId = row.BookingId,
                changeType = row.ChangeType,
                transactionRef = row.TransactionRef,
                payload = row.PayloadJson,
                occurredUtc = DateTime.UtcNow
            };

    private bool IsPartnerWorkflowPayload()
        => _partnerWorkflowOptions.PayloadFormat.Equals("PartnerWorkflow", StringComparison.OrdinalIgnoreCase);

    private static object BuildPartnerWorkflowPayload(DownstreamUpdateModel row)
    {
        using var payload = TryParsePayload(row.PayloadJson);
        var root = payload?.RootElement;

        return new
        {
            transactionId = row.TransactionRef,
            status = MapPartnerStatus(row.ChangeType),
            dateTime = GetString(root, "newStartUtc", "startUtc", "dateTime", "cancelledUtc"),
            meetingType = GetString(root, "meetingType"),
            adviserId = GetString(root, "newAdviserId", "adviserId"),
            notes = GetString(root, "reasonDetail", "reasonNotes", "notes", "reasonCode"),
            bookingReference = GetString(root, "bookingReference") ?? row.TransactionRef
        };
    }

    private static JsonDocument? TryParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            return JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement? root, params string[] names)
    {
        if (root is null || root.Value.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!root.Value.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();

            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return value.ToString();
        }

        return null;
    }

    private static string MapPartnerStatus(string changeType)
        => changeType.Trim().ToLowerInvariant() switch
        {
            "cancel" or "cancelled" or "canceled" => "Cancelled",
            "rearrange" or "rearranged" or "reschedule" or "rescheduled" => "Rescheduled",
            _ => changeType
        };

    private static string BuildIdempotencyKey(DownstreamUpdateModel row)
        => $"{MapIdempotencyPrefix(row.ChangeType)}:{row.BookingId}";

    private static string MapIdempotencyPrefix(string changeType)
        => changeType.Trim().ToLowerInvariant() switch
        {
            "cancel" or "cancelled" or "canceled" => "booking-cancelled",
            "rearrange" or "rearranged" or "reschedule" or "rescheduled" => "booking-rescheduled",
            _ => $"booking-{changeType.Trim().ToLowerInvariant()}"
        };

    private static DownstreamUpdateResponse ToResponse(DownstreamUpdateModel model)
    {
        return new DownstreamUpdateResponse
        {
            UpdateId = model.Id,
            BookingId = model.BookingId,
            ChangeType = model.ChangeType,
            Status = model.Status,
            CreatedUtc = model.CreatedUtc,
            ProcessedUtc = model.ProcessedUtc,
            ErrorMessage = model.ErrorMessage
        };
    }
}
