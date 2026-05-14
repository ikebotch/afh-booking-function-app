namespace AFH.Booking.Infrastructure.Persistence.Models;

public enum HoldStatus
{
    Active = 0,
    Confirmed = 1,
    Released = 2,
    Cancelled = 3,
    Expired = 4
}



public sealed class BookingHoldModel
{
    // -------------------------
    // Identity
    // -------------------------
    public string Id { get; set; } = default!;        // BookingHoldId (string / guid)
    public string UserId { get; set; } = default!;        // BookingHoldId (string / guid)

    // -------------------------
    // Relationships
    // -------------------------
    public string SlotId { get; set; } = default!;    // FK → BookingSlotModel.Id
    public BookingSlotModel Slot { get; set; } = default!;

    // -------------------------
    // Hold lifecycle
    // -------------------------
    public HoldStatus Status { get; set; }     // Active | Confirmed | Cancelled | Expired

    public DateTime CreatedUtc { get; set; }
    public DateTime HoldExpiresUtc { get; set; }

    public DateTime? ConfirmedUtc { get; set; }
    public DateTime? ReleasedUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public string? CancelReason { get; set; }

    // -------------------------
    // Calendar integration
    // -------------------------
    public string? CalendarProviderEventId { get; set; }

    // -------------------------
    // Concurrency
    // -------------------------
    public byte[] RowVersion { get; set; } = default!;
}