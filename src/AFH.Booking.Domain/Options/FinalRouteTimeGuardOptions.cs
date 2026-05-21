namespace AFH.Booking.Domain.Options;

public sealed class FinalRouteTimeGuardOptions
{
    public const string SectionName = "FinalRouteTimeGuard";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Temporary rollout switch for in-person holds created before selected-slot
    /// coordinates were persisted. Keep false for strict final validation.
    /// </summary>
    public bool AllowLegacyMissingCoordinates { get; set; }
}
