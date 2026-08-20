using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class PartnerWorkflowPolicyProvider : IPartnerWorkflowPolicyProvider
{
    private readonly BookingDbContext _db;

    public PartnerWorkflowPolicyProvider(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsEnabledAsync(string changeType, CancellationToken ct)
    {
        var normalized = NormalizeChangeType(changeType);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var row = await _db.PartnerWorkflowRules
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ChangeType == normalized, ct);

        return row?.Enabled == true;
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
