using AFH.Booking.Domain.Common;

namespace AFH.Booking.Domain.Bookings;

public enum BookingTransactionStatus
{
    Open = 0,
    Completed = 1,
    Expired = 2,
    Cancelled = 3
}

public sealed class BookingTransaction
{
    private readonly List<BookingSlot> _slots = new();

    private BookingTransaction() { }

    // Internal PK (DB id)
    public string Id { get; private set; } = default!;

    // External reference (transactionId OR clientId)
    public string TransactionRef { get; private set; } = default!;
    public string? BookingReference { get; private set; }
    public string? ClientName { get; private set; }
    public string? ClientEmail { get; private set; }
    public string? ClientAddressLine1 { get; private set; }
    public string? ClientAddressLine2 { get; private set; }
    public string? ClientTown { get; private set; }
    public string? ClientCounty { get; private set; }
    public string? ClientPostcode { get; private set; }

    // Request details used to generate slots
    public DateTime ProposedStartUtc { get; private set; }
    public TimeSpan Duration { get; private set; }
    public string Timezone { get; private set; } = "Europe/London";

    public bool IsRemote { get; private set; }
    public string? MeetingType { get; private set; }

    // External location reference (optional)
    public string? LocationRef { get; private set; }

    public BookingTransactionStatus Status { get; private set; } = BookingTransactionStatus.Open;

    public DateTime CreatedUtc { get; private set; }
    public DateTime? ExpiresUtc { get; private set; }

    public IReadOnlyList<BookingSlot> Slots => _slots;

    // -----------------------------
    // Factory
    // -----------------------------
    public static BookingTransaction Create(
        string transactionRef,
        DateTime proposedStartUtc,
        TimeSpan duration,
        string timezone,
        bool isRemote,
        string? meetingType,
        string? locationRef,
        DateTime utcNow,
        DateTime? expiresUtc = null)
    {
        if (string.IsNullOrWhiteSpace(transactionRef))
            throw new DomainException("transactionRef is required.");

        if (proposedStartUtc == default)
            throw new DomainException("proposedStartUtc is required.");

        if (duration <= TimeSpan.Zero)
            throw new DomainException("duration must be > 0.");

        if (string.IsNullOrWhiteSpace(timezone))
            throw new DomainException("timezone is required.");

        return new BookingTransaction
        {
            Id = Guid.NewGuid().ToString("N"),
            TransactionRef = transactionRef.Trim(),
            ProposedStartUtc = DateTime.SpecifyKind(proposedStartUtc, DateTimeKind.Utc),
            Duration = duration,
            Timezone = timezone.Trim(),
            IsRemote = isRemote,
            MeetingType = string.IsNullOrWhiteSpace(meetingType) ? null : meetingType.Trim(),
            LocationRef = string.IsNullOrWhiteSpace(locationRef) ? null : locationRef.Trim(),
            Status = BookingTransactionStatus.Open,
            CreatedUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            ExpiresUtc = expiresUtc is null ? null : DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc)
        };
    }

    // -----------------------------
    // Behaviour
    // -----------------------------
    public void AddSlot(BookingSlot slot)
    {
        if (slot is null) throw new ArgumentNullException(nameof(slot));

        EnsureOpen();

  
        if (!string.Equals(slot.TransactionId, Id, StringComparison.Ordinal))
            throw new DomainException("Slot.TransactionId must match BookingTransaction.Id.");

        // Avoid duplicates
        if (_slots.Any(s => string.Equals(s.Id, slot.Id, StringComparison.Ordinal)))
            return;

        _slots.Add(slot);
    }

    public void AssignBookingReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainException("booking reference required.");

        BookingReference = reference.Trim();
    }

    public void CaptureClientSnapshot(
        string? clientName,
        string? clientEmail,
        string? addressLine1,
        string? addressLine2,
        string? town,
        string? county,
        string? postcode)
    {
        ClientName = Normalize(clientName);
        ClientEmail = Normalize(clientEmail);
        ClientAddressLine1 = Normalize(addressLine1);
        ClientAddressLine2 = Normalize(addressLine2);
        ClientTown = Normalize(town);
        ClientCounty = Normalize(county);
        ClientPostcode = Normalize(postcode);
    }

    public BookingSlot? FindSlot(string slotId)
        => _slots.FirstOrDefault(s => string.Equals(s.Id, slotId, StringComparison.Ordinal));

    public void MarkCompleted()
    {
        EnsureNotExpired(DateTime.UtcNow);
        Status = BookingTransactionStatus.Completed;
    }

    public void Cancel()
    {
        Status = BookingTransactionStatus.Cancelled;
    }


    public void ExtendExpiry(DateTime expiresUtc)
    {
        if (expiresUtc <= DateTime.UtcNow)
            throw new DomainException("Transaction expiry must be in the future.");

        ExpiresUtc = expiresUtc;
    }

    public void Expire(DateTime utcNow)
    {
        Status = BookingTransactionStatus.Expired;
        ExpiresUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public bool IsExpired(DateTime utcNow)
        => ExpiresUtc is not null && ExpiresUtc.Value <= utcNow;

    private void EnsureOpen()
    {
        if (Status != BookingTransactionStatus.Open)
            throw new DomainException("Transaction is not open.");
    }

    private void EnsureNotExpired(DateTime utcNow)
    {
        if (IsExpired(utcNow))
            throw new DomainException("Transaction has expired.");
    }

    // -----------------------------
    // Rehydrate (from persistence)
    // -----------------------------
    public static BookingTransaction Rehydrate(
        string id,
        string transactionRef,
        string? bookingReference,
        string? clientName,
        string? clientEmail,
        string? clientAddressLine1,
        string? clientAddressLine2,
        string? clientTown,
        string? clientCounty,
        string? clientPostcode,
        DateTime proposedStartUtc,
        TimeSpan duration,
        string timezone,
        bool isRemote,
        string? meetingType,
        string? locationRef,
        BookingTransactionStatus status,
        DateTime createdUtc,
        DateTime? expiresUtc,
        IEnumerable<BookingSlot>? slots = null)
    {
        var tx = new BookingTransaction
        {
            Id = id,
            TransactionRef = transactionRef,
            BookingReference = bookingReference,
            ClientName = clientName,
            ClientEmail = clientEmail,
            ClientAddressLine1 = clientAddressLine1,
            ClientAddressLine2 = clientAddressLine2,
            ClientTown = clientTown,
            ClientCounty = clientCounty,
            ClientPostcode = clientPostcode,
            ProposedStartUtc = DateTime.SpecifyKind(proposedStartUtc, DateTimeKind.Utc),
            Duration = duration,
            Timezone = timezone,
            IsRemote = isRemote,
            MeetingType = meetingType,
            LocationRef = locationRef,
            Status = status,
            CreatedUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc),
            ExpiresUtc = expiresUtc is null ? null : DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc)
        };

        if (slots is not null)
        {
            foreach (var s in slots)
            {
                // no EnsureOpen here — rehydration must accept persisted state
                if (!tx._slots.Any(x => string.Equals(x.Id, s.Id, StringComparison.Ordinal)))
                    tx._slots.Add(s);
            }
        }

        return tx;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
