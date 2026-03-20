namespace AFH.Booking.Domain.Bookings;

public enum BookingHoldStatus
{
    Active = 0,
    Confirmed = 1,
    Released = 2,
    Cancelled = 3,
    Expired = 4
}

public sealed class BookingHold
{
    private BookingHold() { }

    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string SlotId { get; private set; } = default!;
    public BookingHoldStatus Status { get; private set; }

    public DateTime CreatedUtc { get; private set; }
    public DateTime ExpiresUtc { get; private set; }

    public DateTime? ConfirmedUtc { get; private set; }
    public DateTime? ReleasedUtc { get; private set; }
    public DateTime? CancelledUtc { get; private set; }
    public string? CancelReason { get; private set; }

    public string? CalendarProviderEventId { get; private set; }

    public static BookingHold Create(
        string slotId,
        string userId,
        TimeSpan holdDuration,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            throw new DomainException("slotId required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("userId required.");

        if (holdDuration <= TimeSpan.Zero)
            throw new DomainException("holdDuration must be > 0.");


        utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        return new BookingHold
        {
            Id = Guid.NewGuid().ToString("N"),
            SlotId = slotId,
            UserId = userId,
            Status = BookingHoldStatus.Active,
            CreatedUtc = utcNow,
            ExpiresUtc = utcNow.Add(holdDuration)
        };
    }

    public void Confirm(DateTime utcNow)
    {
        EnsureActive();

        if (utcNow >= ExpiresUtc)
            throw new DomainException("Hold expired.");

        Status = BookingHoldStatus.Confirmed;
        ConfirmedUtc = utcNow;
    }

    public void Release(DateTime utcNow)
    {
        if (Status == BookingHoldStatus.Released)
            return;

        if (Status == BookingHoldStatus.Confirmed)
            throw new DomainException("Cannot release confirmed hold.");

        Status = BookingHoldStatus.Released;
        ReleasedUtc = utcNow;
    }

    public void Cancel(string reason, DateTime utcNow)
    {
        if (Status == BookingHoldStatus.Cancelled)
            return;

        if (Status == BookingHoldStatus.Expired)
            throw new DomainException("Cannot cancel.");

        Status = BookingHoldStatus.Cancelled;
        CancelledUtc = utcNow;
        CancelReason = reason;
    }

    public void Expire(DateTime utcNow)
    {
        if (Status != BookingHoldStatus.Active)
            return;

        if (utcNow >= ExpiresUtc)
            Status = BookingHoldStatus.Expired;
    }

    private void EnsureActive()
    {
        if (Status != BookingHoldStatus.Active)
            throw new DomainException("Only active holds allowed.");
    }

    public void AttachCalendarEvent(string? eventId)
    {
        CalendarProviderEventId = eventId;
    }

    public static BookingHold Rehydrate(
        string id,
        string slotId,
        string userid,
        BookingHoldStatus status,
        DateTime createdUtc,
        DateTime expiresUtc,
        DateTime? confirmedUtc,
        DateTime? releasedUtc,
        DateTime? cancelledUtc,
        string? cancelReason,
        string? providerEventId)
    {
        return new BookingHold
        {
            Id = id,
            SlotId = slotId,
            Status = status,
            CreatedUtc = createdUtc,
            ExpiresUtc = expiresUtc,
            ConfirmedUtc = confirmedUtc,
            ReleasedUtc = releasedUtc,
            CancelledUtc = cancelledUtc,
            CancelReason = cancelReason,
            CalendarProviderEventId = providerEventId,
            UserId = userid
        };
    }
}
