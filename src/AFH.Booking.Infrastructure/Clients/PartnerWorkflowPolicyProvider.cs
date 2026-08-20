using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class PartnerWorkflowPolicyProvider : IPartnerWorkflowPolicyProvider
{
    private readonly BookingDbContext _db;

    public PartnerWorkflowPolicyProvider(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PartnerWorkflowSendPolicy>> ListAsync(string changeType, CancellationToken ct)
    {
        var normalized = NormalizeChangeType(changeType);
        if (string.IsNullOrWhiteSpace(normalized))
            return [new PartnerWorkflowSendPolicy(false, string.Empty, null, null)];

        var rows = await _db.PartnerWorkflowRules
            .AsNoTracking()
            .Include(x => x.Endpoint)
            .Where(x => x.ChangeType == normalized)
            .OrderBy(x => x.PartnerKey)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return [new PartnerWorkflowSendPolicy(false, normalized, null, null)];

        return rows.Select(row => ToPolicy(row, normalized)).ToList();
    }

    public async Task<PartnerWorkflowSendPolicy> GetAsync(string changeType, string? partnerKey, CancellationToken ct)
    {
        var normalized = NormalizeChangeType(changeType);
        if (string.IsNullOrWhiteSpace(normalized))
            return new PartnerWorkflowSendPolicy(false, string.Empty, partnerKey, null);

        var query = _db.PartnerWorkflowRules
            .AsNoTracking()
            .Include(x => x.Endpoint)
            .Where(x => x.ChangeType == normalized);

        if (!string.IsNullOrWhiteSpace(partnerKey))
            query = query.Where(x => x.PartnerKey == partnerKey);

        var row = await query
            .OrderBy(x => x.PartnerKey)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? new PartnerWorkflowSendPolicy(false, normalized, partnerKey, null)
            : ToPolicy(row, normalized);
    }

    private static PartnerWorkflowSendPolicy ToPolicy(PartnerWorkflowRuleModel row, string normalized)
    {
        if (row.Endpoint?.Enabled != true)
            return new PartnerWorkflowSendPolicy(row.Enabled, normalized, row.PartnerKey, null);

        var endpoint = new PartnerWorkflowEndpoint(
            row.Endpoint.PartnerKey,
            row.Endpoint.DisplayName,
            row.Endpoint.BookingUpdatesUrl,
            row.Endpoint.BaseUrl,
            row.Endpoint.BookingUpdatesPath,
            row.Endpoint.ApiKey,
            row.Endpoint.ApiKeyHeaderName,
            row.Endpoint.IdempotencyKeyHeaderName,
            row.Endpoint.PayloadFormat);

        return new PartnerWorkflowSendPolicy(row.Enabled, normalized, row.PartnerKey, endpoint);
    }

    public static string NormalizeChangeType(string changeType)
        => changeType.Trim().ToLowerInvariant() switch
        {
            "booked" or "bookingconfirmed" or "confirmed" or "confirm" => "Booked",
            "cancel" or "cancelled" or "canceled" or "bookingcancelled" => "Cancel",
            "rearrange" or "rearranged" or "reschedule" or "rescheduled" or "bookingrescheduled" => "Rearrange",
            var value => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()
        };
}
