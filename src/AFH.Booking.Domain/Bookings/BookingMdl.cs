using AFH.Booking.Domain.Common;
using AFH.Booking.Domain.ValueObjects;

namespace AFH.Booking.Domain.Bookings;

public sealed class BookingMdl
{
    // EF-friendly private ctor (even though EF maps infra model, this keeps options open)
    private BookingMdl() { }

    public BookingId Id { get; private set; } = BookingId.New();

    public string AdviserId { get; private set; } = default!;
    public string CustomerId { get; private set; } = default!;
    public string? Subject { get; private set; }

    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public string Timezone { get; private set; } = default!;

    public MeetingMode Mode { get; private set; }
    public BookingStatus Status { get; private set; }

    public string? Notes { get; private set; }
    public string? TransactionId { get; private set; }

    // Calendar linkage (provider event id)
    public string? CalendarProviderEventId { get; private set; }

    // Hold-specific
    public DateTime? HoldExpiresUtc { get; private set; }

    // Audit
    public DateTime CreatedUtc { get; private set; }

    // Optional domain “location” (NOT a DTO)
    public BookingLocation? Location { get; private set; }
    public string? ProviderEventId { get; set; }
    // ----------------------------
    // Factory
    // ----------------------------
    public static BookingMdl CreateHold(
        string adviserId,
        string customerId,
        string? subject,
        DateTime startUtc,
        DateTime endUtc,
        string timezone,
        MeetingMode mode,
        TimeSpan holdDuration,
        DateTime utcNow,
        string? notes = null,
        string? transactionId = null,
        BookingLocation? location = null)
    {
        adviserId = Guard.NotNullOrWhiteSpace(adviserId, nameof(adviserId));
        customerId = Guard.NotNullOrWhiteSpace(customerId, nameof(customerId));
        timezone = Guard.NotNullOrWhiteSpace(timezone, nameof(timezone));

        Guard.True(endUtc > startUtc, "endUtc must be after startUtc.");
        Guard.True(holdDuration > TimeSpan.Zero, "Hold duration must be > 0.");

        var b = new BookingMdl
        {
            Id = BookingId.New(),
            AdviserId = adviserId,
            CustomerId = customerId,
            Subject = subject,
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            Timezone = timezone,
            Mode = mode,
            Status = BookingStatus.Hold,
            Notes = notes,
            TransactionId = transactionId,
            CreatedUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            HoldExpiresUtc = HoldWindow.ComputeHoldExpiryUtc(utcNow, holdDuration),
            Location = location
        };

        return b;
    }

    // ----------------------------
    // Rehydrate (for repository mapping)
    // ----------------------------
    public static BookingMdl Rehydrate(
        BookingId id,
        string adviserId,
        string customerId,
        string? subject,
        DateTime startUtc,
        DateTime endUtc,
        string timezone,
        MeetingMode mode,
        BookingStatus status,
        string? providerEventId,
        string? transactionId,
        string? calendarProviderEventId,
        string? notes,
        DateTime? holdExpiresUtc,
        DateTime createdUtc,
        BookingLocation? location
        
        )
    {
        adviserId = Guard.NotNullOrWhiteSpace(adviserId, nameof(adviserId));
        customerId = Guard.NotNullOrWhiteSpace(customerId, nameof(customerId));
        timezone = Guard.NotNullOrWhiteSpace(timezone, nameof(timezone));
        Guard.True(endUtc > startUtc, "endUtc must be after startUtc.");

        return new BookingMdl
        {
            Id = id,
            AdviserId = adviserId,
            CustomerId = customerId,
            Subject = subject,
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            Timezone = timezone,
            Mode = mode,
            Status = status,
            Notes = notes,
            TransactionId = transactionId,
            CalendarProviderEventId = calendarProviderEventId,
            HoldExpiresUtc = holdExpiresUtc,
            CreatedUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc),
            Location = location
        };
    }

    // ----------------------------
    // Behaviour
    // ----------------------------
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
        providerEventId = Guard.NotNullOrWhiteSpace(providerEventId, nameof(providerEventId));
        CalendarProviderEventId = providerEventId;
    }

    public bool IsHoldActive(DateTime utcNow)
        => Status == BookingStatus.Hold && (HoldExpiresUtc is null || HoldExpiresUtc > utcNow);

    public TimeRange ToTimeRange()
        => new TimeRange(StartUtc, EndUtc);

    public void UpdateLocation(BookingLocation? location)
    {
        Location = location;
    }
}
