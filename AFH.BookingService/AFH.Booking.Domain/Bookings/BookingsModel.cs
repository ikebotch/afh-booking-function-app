
using AFH.Booking.Domain.Common;
using AFH.Common.CalendarUtils.Contracts.Enums;



namespace AFH.Booking.Domain.Bookings;

public  class BookingsModel
{
    // EF Core
    public BookingsModel() { }

    public BookingId Id { get; set; } = BookingId.New();

    public string AdviserId { get;  set; } = default!;
    public string CustomerId { get;  set; } = default!;
    public string Subject { get; set; } = default!;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Timezone { get; set; } = default!;

    public MeetingMode Mode { get; set; }
    public BookingStatus Status { get; set; }

    public string? Notes { get; set; }

    // Calendar integration (opaque to domain)
    public string? ProviderEventId { get; set; }

    public DateTime? HoldExpiresUtc { get; set; }

    public Location Location { get; set; }

    public string? TransactionId { get; set; }
    public TimeSpan HoldDuration { get; init; }

    public DateTime CreatedUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public bool IsRemote { get; set; }
    public IEnumerable<string>? Categories { get; set; }
    public CalendarImportance Importance { get; set; } = CalendarImportance.Normal;



    /* ---------------- Domain behaviour ---------------- */

    public void Confirm(DateTime utcNow)
    {
        Guard.True(Status == BookingStatus.Hold, "Only held bookings can be confirmed.");

        if (HoldExpiresUtc is not null)
            Guard.True(HoldExpiresUtc > utcNow, "Hold has expired.");

        Status = BookingStatus.Confirmed;
        HoldExpiresUtc = null;
    }

    public void Cancel(string? reason = null)
    {
        if (Status == BookingStatus.Cancelled)
            return;

        Status = BookingStatus.Cancelled;
        HoldExpiresUtc = null;

        if (!string.IsNullOrWhiteSpace(reason))
            Notes = reason;
    }

    public void AttachCalendarEvent(string providerEventId)
    {
        ProviderEventId =
            Guard.NotNullOrWhiteSpace(providerEventId, nameof(providerEventId));
    }

    public bool IsHoldActive(DateTime utcNow)
        => Status == BookingStatus.Hold &&
           (HoldExpiresUtc is null || HoldExpiresUtc > utcNow);

    public TimeRange ToTimeRange()
        => new(StartUtc, EndUtc);
}