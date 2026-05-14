using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.ValueObjects;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence.Mapping;

public static class BookingHoldMapping
{
    public static BookingHoldModel ToModel(this BookingHold h) => new()
    {
        Id = h.Id,
        SlotId = h.SlotId,
        UserId = h.UserId,

        Status = ToModelStatus(h.Status),

        CreatedUtc = AsUtc(h.CreatedUtc),
        HoldExpiresUtc = AsUtc(h.ExpiresUtc),

        ConfirmedUtc = AsUtcNullable(h.ConfirmedUtc),
        ReleasedUtc = AsUtcNullable(h.ReleasedUtc),
        CancelledUtc = AsUtcNullable(h.CancelledUtc),

        CancelReason = h.CancelReason,
        CalendarProviderEventId = h.CalendarProviderEventId
    };

    public static BookingHold ToDomain(this BookingHoldModel m) =>
        BookingHold.Rehydrate(
            id: m.Id,
            slotId: m.SlotId,
            userid: m.UserId,
            status: ToDomainStatus(m.Status),
            createdUtc: AsUtc(m.CreatedUtc),
            expiresUtc: AsUtc(m.HoldExpiresUtc),
            confirmedUtc: AsUtcNullable(m.ConfirmedUtc),
            releasedUtc: AsUtcNullable(m.ReleasedUtc),
            cancelledUtc: AsUtcNullable(m.CancelledUtc),
            cancelReason: m.CancelReason,
            providerEventId: m.CalendarProviderEventId,
            bookingId: m.Slot?.TransactionId
        );

    private static HoldStatus ToModelStatus(BookingHoldStatus s) => s switch
    {
        BookingHoldStatus.Active => HoldStatus.Active,
        BookingHoldStatus.Confirmed => HoldStatus.Confirmed,
        BookingHoldStatus.Released => HoldStatus.Released,
        BookingHoldStatus.Cancelled => HoldStatus.Cancelled,
        BookingHoldStatus.Expired => HoldStatus.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown BookingHoldStatus")
    };

    private static BookingHoldStatus ToDomainStatus(HoldStatus s) => s switch
    {
        HoldStatus.Active => BookingHoldStatus.Active,
        HoldStatus.Confirmed => BookingHoldStatus.Confirmed,
        HoldStatus.Released => BookingHoldStatus.Released,
        HoldStatus.Cancelled => BookingHoldStatus.Cancelled,
        HoldStatus.Expired => BookingHoldStatus.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown HoldStatus")
    };

    public static void ApplyToModel(this BookingHold h, BookingHoldModel m)
    {
        if (h is null) throw new ArgumentNullException(nameof(h));
        if (m is null) throw new ArgumentNullException(nameof(m));

        // SlotId should not change for a hold
        // m.SlotId = h.SlotId;

        m.Status = (Models.HoldStatus)h.Status;

        m.CreatedUtc = AsUtc(h.CreatedUtc);
        m.HoldExpiresUtc = AsUtc(h.ExpiresUtc);
        m.ConfirmedUtc = h.ConfirmedUtc;
        m.CancelledUtc = h.CancelledUtc;
        m.CancelReason = h.CancelReason;

        m.CalendarProviderEventId = h.CalendarProviderEventId;
    }

    private static DateTime AsUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static DateTime? AsUtcNullable(DateTime? dt) =>
        dt is null ? null : AsUtc(dt.Value);
}