using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Options;
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
    private readonly XPlanOptions _xPlanOptions;
    private readonly ILogger<DownstreamUpdateService> _logger;

    public DownstreamUpdateService(
        BookingDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<XPlanOptions> xPlanOptions,
        ILogger<DownstreamUpdateService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _xPlanOptions = xPlanOptions.Value;
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
        if (!_xPlanOptions.Enabled || string.IsNullOrWhiteSpace(_xPlanOptions.BaseUrl))
        {
            row.Status = "ConfiguredOff";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
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

            var http = _httpClientFactory.CreateClient("xplan-updates");
            http.BaseAddress = new Uri(_xPlanOptions.BaseUrl, UriKind.Absolute);
            if (!string.IsNullOrWhiteSpace(_xPlanOptions.ApiKey))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _xPlanOptions.ApiKey);

            var payload = new
            {
                bookingId = row.BookingId,
                changeType = row.ChangeType,
                transactionRef = row.TransactionRef,
                payload = row.PayloadJson,
                occurredUtc = DateTime.UtcNow
            };

            var response = await http.PostAsJsonAsync("/api/booking-updates", payload, ct);
            row.Status = response.IsSuccessStatusCode ? "Sent" : "Failed";
            row.ErrorMessage = response.IsSuccessStatusCode ? null : $"XPlan responded {(int)response.StatusCode}";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Downstream booking change failed. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} StatusCode={StatusCode} CorrelationId={CorrelationId}",
                    row.Id,
                    row.BookingId,
                    row.ChangeType,
                    (int)response.StatusCode,
                    correlationId);
            }
        }
        catch (Exception ex)
        {
            row.Status = "Failed";
            row.ErrorMessage = ex.Message;
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                ex,
                "Downstream booking change threw. UpdateId={UpdateId} BookingId={BookingId} ChangeType={ChangeType} AttemptCount={AttemptCount} CorrelationId={CorrelationId}",
                row.Id,
                row.BookingId,
                row.ChangeType,
                row.AttemptCount,
                correlationId);
        }
    }

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
