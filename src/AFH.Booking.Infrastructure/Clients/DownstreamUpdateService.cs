using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class DownstreamUpdateService : IDownstreamUpdateService
{
    private readonly BookingDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly XPlanOptions _xPlanOptions;

    public DownstreamUpdateService(
        BookingDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<XPlanOptions> xPlanOptions)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _xPlanOptions = xPlanOptions.Value;
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

        if (!_xPlanOptions.Enabled || string.IsNullOrWhiteSpace(_xPlanOptions.BaseUrl))
        {
            row.Status = "ConfiguredOff";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return ToResponse(row);
        }

        try
        {
            var http = _httpClientFactory.CreateClient("xplan-updates");
            http.BaseAddress = new Uri(_xPlanOptions.BaseUrl, UriKind.Absolute);
            if (!string.IsNullOrWhiteSpace(_xPlanOptions.ApiKey))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _xPlanOptions.ApiKey);

            var payload = new
            {
                bookingId,
                changeType,
                transactionRef,
                payload = payloadJson,
                occurredUtc = DateTime.UtcNow
            };

            var response = await http.PostAsJsonAsync("/api/booking-updates", payload, ct);
            row.Status = response.IsSuccessStatusCode ? "Sent" : "Failed";
            row.ErrorMessage = response.IsSuccessStatusCode ? null : $"XPlan responded {(int)response.StatusCode}";
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            row.Status = "Failed";
            row.ErrorMessage = ex.Message;
            row.ProcessedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return ToResponse(row);
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
