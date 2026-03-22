namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class IntegrationSyncStateModel
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}
