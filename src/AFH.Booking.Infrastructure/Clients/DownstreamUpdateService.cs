using System.Net.Http.Headers;
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
        var policies = await _policyProvider.ListAsync(changeType, ct);
        var rows = policies.Select(policy => new DownstreamUpdateModel
            {
                Id = Guid.NewGuid().ToString("N"),
                BookingId = bookingId,
                ChangeType = policy.NormalizedChangeType,
                PartnerKey = policy.PartnerKey ?? policy.Endpoint?.PartnerKey,
                TransactionRef = transactionRef,
                PayloadJson = payloadJson,
                Status = "Pending",
                AttemptCount = 1,
                CreatedUtc = DateTime.UtcNow
            })
            .ToList();

        _db.DownstreamUpdates.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        for (var i = 0; i < rows.Count; i++)
            await DispatchAsync(rows[i], policies[i], correlationId: null, ct);

        return ToResponse(rows);
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
                PartnerKey = row.PartnerKey,
                PreviousStatus = previousStatus,
                CurrentStatus = row.Status,
                AttemptCount = row.AttemptCount,
                ProcessedUtc = row.ProcessedUtc,
                ErrorMessage = row.ErrorMessage,
                ResponseStatusCode = row.ResponseStatusCode,
                ResponseReceivedUtc = row.ResponseReceivedUtc
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
        var policy = await _policyProvider.GetAsync(row.ChangeType, row.PartnerKey, ct);
        await DispatchAsync(row, policy, correlationId, ct);
    }

    private async Task DispatchAsync(
        DownstreamUpdateModel row,
        PartnerWorkflowSendPolicy policy,
        string? correlationId,
        CancellationToken ct)
    {
        if (!_partnerWorkflowOptions.Enabled)
        {
            await MarkConfiguredOffAsync(row, correlationId, "PartnerWorkflow:MasterDisabled", policy, endpoint: null, hasUpdateUri: false, ct);
            return;
        }

        if (!policy.WorkflowEnabled)
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
                    row.PartnerKey,
                    row.TransactionRef,
                    row.ErrorMessage
                },
                ct);
            return;
        }

        var endpoint = policy.Endpoint;
        Uri? updateUri = null;
        var hasUpdateUri = endpoint is not null && TryResolveUpdateUri(endpoint, out updateUri);
        if (endpoint is null || !hasUpdateUri)
        {
            await MarkConfiguredOffAsync(row, correlationId, "PartnerWorkflow:EndpointMissingOrInvalid", policy, endpoint, hasUpdateUri, ct);
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

            row.PartnerKey ??= endpoint.PartnerKey;
            var http = _httpClientFactory.CreateClient("partner-workflow-updates");
            using var request = new HttpRequestMessage(HttpMethod.Post, updateUri);
            AddApiKeyHeader(request, endpoint);
            var idempotencyKey = BuildIdempotencyKey(row);
            AddIdempotencyHeader(request, endpoint, idempotencyKey);
            var outboundPayload = BuildPayload(row, endpoint);
            var outboundJson = JsonSerializer.Serialize(outboundPayload);
            request.Content = new StringContent(
                outboundJson,
                Encoding.UTF8,
                "application/json");

            var response = await http.SendAsync(request, ct);
            var responseBody = response.Content is null
                ? null
                : await response.Content.ReadAsStringAsync(ct);
            row.Status = response.IsSuccessStatusCode ? "Sent" : "Failed";
            row.ErrorMessage = response.IsSuccessStatusCode
                ? null
                : $"PartnerWorkflow:{DownstreamFailureClassifier.Classify(response.StatusCode)}:{(int)response.StatusCode}";
            row.ResponseStatusCode = (int)response.StatusCode;
            row.ResponseBody = TruncateResponseBody(responseBody);
            row.ResponseReceivedUtc = DateTime.UtcNow;
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
                    row.PartnerKey,
                    row.TransactionRef,
                    EndpointPartnerKey = endpoint.PartnerKey,
                    EndpointUrl = updateUri!.AbsoluteUri,
                    IdempotencyKey = idempotencyKey,
                    Payload = outboundPayload,
                    Curl = BuildCurlPreview(updateUri, endpoint, idempotencyKey, outboundJson),
                    StatusCode = (int)response.StatusCode,
                    ResponseBody = row.ResponseBody,
                    row.ResponseReceivedUtc,
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
            row.ResponseStatusCode = null;
            row.ResponseBody = ex.Message;
            row.ResponseReceivedUtc = DateTime.UtcNow;
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
                    row.PartnerKey,
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

    private async Task MarkConfiguredOffAsync(
        DownstreamUpdateModel row,
        string? correlationId,
        string reason,
        PartnerWorkflowSendPolicy policy,
        PartnerWorkflowEndpoint? endpoint,
        bool hasUpdateUri,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Partner workflow booking change configured off. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} Reason={Reason} MasterEnabled={MasterEnabled} WorkflowEnabled={WorkflowEnabled} PartnerKey={PartnerKey} HasBookingUpdatesUrl={HasBookingUpdatesUrl} HasBaseUrl={HasBaseUrl} HasResolvedUpdateUri={HasResolvedUpdateUri} CorrelationId={CorrelationId}",
            row.Id,
            row.BookingId,
            row.ChangeType,
            reason,
            _partnerWorkflowOptions.Enabled,
            policy.WorkflowEnabled,
            endpoint?.PartnerKey,
            !string.IsNullOrWhiteSpace(endpoint?.BookingUpdatesUrl),
            !string.IsNullOrWhiteSpace(endpoint?.BaseUrl),
            hasUpdateUri,
            correlationId);

        row.Status = "ConfiguredOff";
        row.ErrorMessage = reason;
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
                row.PartnerKey,
                row.TransactionRef,
                Reason = reason,
                MasterEnabled = _partnerWorkflowOptions.Enabled,
                policy.WorkflowEnabled,
                EndpointPartnerKey = endpoint?.PartnerKey,
                EndpointDisplayName = endpoint?.DisplayName,
                HasBookingUpdatesUrl = !string.IsNullOrWhiteSpace(endpoint?.BookingUpdatesUrl),
                HasBaseUrl = !string.IsNullOrWhiteSpace(endpoint?.BaseUrl),
                HasResolvedUpdateUri = hasUpdateUri
            },
            ct);
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

    private static bool TryResolveUpdateUri(PartnerWorkflowEndpoint endpoint, out Uri updateUri)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.BookingUpdatesUrl) &&
            Uri.TryCreate(endpoint.BookingUpdatesUrl.Trim(), UriKind.Absolute, out updateUri!))
        {
            return true;
        }

        updateUri = null!;
        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
            return false;

        var baseUri = endpoint.BaseUrl.TrimEnd('/') + "/";
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var parsedBaseUri))
            return false;

        var path = string.IsNullOrWhiteSpace(endpoint.BookingUpdatesPath)
            ? "/api/booking-updates"
            : endpoint.BookingUpdatesPath.Trim();

        updateUri = new Uri(parsedBaseUri, path.TrimStart('/'));
        return true;
    }

    private static void AddApiKeyHeader(HttpRequestMessage request, PartnerWorkflowEndpoint endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.ApiKey))
            return;

        var headerName = string.IsNullOrWhiteSpace(endpoint.ApiKeyHeaderName)
            ? "Authorization"
            : endpoint.ApiKeyHeaderName.Trim();

        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey.Trim());
            return;
        }

        request.Headers.TryAddWithoutValidation(headerName, endpoint.ApiKey.Trim());
    }

    private static void AddIdempotencyHeader(HttpRequestMessage request, PartnerWorkflowEndpoint endpoint, string idempotencyKey)
    {
        var headerName = string.IsNullOrWhiteSpace(endpoint.IdempotencyKeyHeaderName)
            ? "X-Idempotency-Key"
            : endpoint.IdempotencyKeyHeaderName.Trim();

        request.Headers.TryAddWithoutValidation(headerName, idempotencyKey);
    }

    private static object BuildPayload(DownstreamUpdateModel row, PartnerWorkflowEndpoint endpoint)
        => IsPartnerWorkflowPayload(endpoint)
            ? BuildPartnerWorkflowPayload(row)
            : new
            {
                bookingId = row.BookingId,
                changeType = row.ChangeType,
                transactionRef = row.TransactionRef,
                payload = row.PayloadJson,
                occurredUtc = DateTime.UtcNow
            };

    private static bool IsPartnerWorkflowPayload(PartnerWorkflowEndpoint endpoint)
        => endpoint.PayloadFormat.Equals("PartnerWorkflow", StringComparison.OrdinalIgnoreCase);

    private static object BuildPartnerWorkflowPayload(DownstreamUpdateModel row)
    {
        using var payload = TryParsePayload(row.PayloadJson);
        var root = payload?.RootElement;

        return new
        {
            transactionId = row.TransactionRef,
            transactionRef = GetString(root, "transactionRef") ?? row.TransactionRef,
            status = MapPartnerStatus(row.ChangeType),
            dateTime = GetString(root, "newStartUtc", "startUtc", "dateTime", "cancelledUtc"),
            meetingType = GetString(root, "meetingType"),
            meetingMode = GetString(root, "meetingMode"),
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

    private static string BuildCurlPreview(
        Uri endpointUri,
        PartnerWorkflowEndpoint endpoint,
        string idempotencyKey,
        string outboundJson)
    {
        var apiKeyHeaderName = string.IsNullOrWhiteSpace(endpoint.ApiKeyHeaderName)
            ? "Authorization"
            : endpoint.ApiKeyHeaderName.Trim();
        var idempotencyHeaderName = string.IsNullOrWhiteSpace(endpoint.IdempotencyKeyHeaderName)
            ? "X-Idempotency-Key"
            : endpoint.IdempotencyKeyHeaderName.Trim();
        var apiKeyValue = apiKeyHeaderName.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
            ? "Bearer <redacted>"
            : "<redacted>";

        return "curl -X POST " +
               $"\"{endpointUri.AbsoluteUri}\" " +
               "-H \"Content-Type: application/json\" " +
               $"-H \"{apiKeyHeaderName}: {apiKeyValue}\" " +
               $"-H \"{idempotencyHeaderName}: {idempotencyKey}\" " +
               $"-d '{outboundJson.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    private static string? TruncateResponseBody(string? responseBody)
    {
        if (string.IsNullOrEmpty(responseBody))
            return responseBody;

        const int maxLength = 16_000;
        return responseBody.Length <= maxLength
            ? responseBody
            : responseBody[..maxLength];
    }

    private static DownstreamUpdateResponse ToResponse(IReadOnlyList<DownstreamUpdateModel> rows)
    {
        if (rows.Count == 1)
            return ToResponse(rows[0]);

        var first = rows[0];
        return new DownstreamUpdateResponse
        {
            UpdateId = string.Join(",", rows.Select(x => x.Id)),
            BookingId = first.BookingId,
            ChangeType = first.ChangeType,
            PartnerKey = null,
            Status = SummarizeStatus(rows),
            CreatedUtc = rows.Min(x => x.CreatedUtc),
            ProcessedUtc = rows.All(x => x.ProcessedUtc is not null) ? rows.Max(x => x.ProcessedUtc) : null,
            ErrorMessage = string.Join("; ", rows.Where(x => !string.IsNullOrWhiteSpace(x.ErrorMessage)).Select(x => $"{x.PartnerKey ?? "unknown"}:{x.ErrorMessage}")),
            ResponseStatusCode = rows.Count == 1 ? rows[0].ResponseStatusCode : null,
            ResponseReceivedUtc = rows.All(x => x.ResponseReceivedUtc is not null) ? rows.Max(x => x.ResponseReceivedUtc) : null
        };
    }

    private static DownstreamUpdateResponse ToResponse(DownstreamUpdateModel model)
    {
        return new DownstreamUpdateResponse
        {
            UpdateId = model.Id,
            BookingId = model.BookingId,
            ChangeType = model.ChangeType,
            PartnerKey = model.PartnerKey,
            Status = model.Status,
            CreatedUtc = model.CreatedUtc,
            ProcessedUtc = model.ProcessedUtc,
            ErrorMessage = model.ErrorMessage,
            ResponseStatusCode = model.ResponseStatusCode,
            ResponseReceivedUtc = model.ResponseReceivedUtc
        };
    }

    private static string SummarizeStatus(IReadOnlyList<DownstreamUpdateModel> rows)
    {
        if (rows.All(x => string.Equals(x.Status, "Sent", StringComparison.OrdinalIgnoreCase)))
            return "Sent";
        if (rows.Any(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
            return "Failed";
        if (rows.All(x => string.Equals(x.Status, "Skipped", StringComparison.OrdinalIgnoreCase)))
            return "Skipped";
        if (rows.All(x => string.Equals(x.Status, "ConfiguredOff", StringComparison.OrdinalIgnoreCase)))
            return "ConfiguredOff";
        return "Partial";
    }
}
