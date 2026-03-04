namespace AFH.Booking.Domain.Calendar;

public sealed class CalendarEventSnapshot
{
    private CalendarEventSnapshot() { }

    public string Id { get; private set; } = default!;
    public string ReceiptId { get; private set; } = default!;

    public string UserId { get; private set; } = default!;
    public string ProviderEventId { get; private set; } = default!;

    public string? CalendarId { get; private set; }
    public string? Subject { get; private set; }
    public DateTime? StartUtc { get; private set; }
    public DateTime? EndUtc { get; private set; }
    public bool? IsCancelled { get; private set; }

    public string? ChangeKey { get; private set; }
    public string? ICalUId { get; private set; }

    public DateTime FetchedUtc { get; private set; }
    public string? FetchError { get; private set; }

    public bool FetchSucceeded => string.IsNullOrWhiteSpace(FetchError);

    public static CalendarEventSnapshot CreateSuccess(
        string id,
        string receiptId,
        string userId,
        string providerEventId,
        string? calendarId,
        string? subject,
        DateTime? startUtc,
        DateTime? endUtc,
        bool? isCancelled,
        string? changeKey,
        string? iCalUId,
        DateTime fetchedUtc)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new DomainException("snapshot id is required.");
        if (string.IsNullOrWhiteSpace(receiptId)) throw new DomainException("receiptId is required.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("userId is required.");
        if (string.IsNullOrWhiteSpace(providerEventId)) throw new DomainException("providerEventId is required.");

        return new CalendarEventSnapshot
        {
            Id = id,
            ReceiptId = receiptId,
            UserId = userId,
            ProviderEventId = providerEventId,
            CalendarId = string.IsNullOrWhiteSpace(calendarId) ? null : calendarId.Trim(),
            Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
            StartUtc = startUtc.HasValue ? DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc) : null,
            EndUtc = endUtc.HasValue ? DateTime.SpecifyKind(endUtc.Value, DateTimeKind.Utc) : null,
            IsCancelled = isCancelled,
            ChangeKey = string.IsNullOrWhiteSpace(changeKey) ? null : changeKey.Trim(),
            ICalUId = string.IsNullOrWhiteSpace(iCalUId) ? null : iCalUId.Trim(),
            FetchedUtc = DateTime.SpecifyKind(fetchedUtc, DateTimeKind.Utc),
            FetchError = null
        };
    }

    public static CalendarEventSnapshot CreateFailure(
        string id,
        string receiptId,
        string userId,
        string providerEventId,
        string fetchError,
        DateTime fetchedUtc,
        string? changeKey = null,
        string? iCalUId = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new DomainException("snapshot id is required.");
        if (string.IsNullOrWhiteSpace(receiptId)) throw new DomainException("receiptId is required.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("userId is required.");
        if (string.IsNullOrWhiteSpace(providerEventId)) throw new DomainException("providerEventId is required.");
        if (string.IsNullOrWhiteSpace(fetchError)) throw new DomainException("fetchError is required.");

        return new CalendarEventSnapshot
        {
            Id = id,
            ReceiptId = receiptId,
            UserId = userId,
            ProviderEventId = providerEventId,
            CalendarId = null,
            Subject = null,
            StartUtc = null,
            EndUtc = null,
            IsCancelled = null,
            ChangeKey = string.IsNullOrWhiteSpace(changeKey) ? null : changeKey.Trim(),
            ICalUId = string.IsNullOrWhiteSpace(iCalUId) ? null : iCalUId.Trim(),
            FetchedUtc = DateTime.SpecifyKind(fetchedUtc, DateTimeKind.Utc),
            FetchError = fetchError.Trim()
        };
    }

    public static CalendarEventSnapshot Rehydrate(
        string id,
        string receiptId,
        string userId,
        string providerEventId,
        string? calendarId,
        string? subject,
        DateTime? startUtc,
        DateTime? endUtc,
        bool? isCancelled,
        DateTime fetchedUtc,
        string? fetchError,
        string? changeKey,
        string? iCalUId)
    {
        return new CalendarEventSnapshot
        {
            Id = id,
            ReceiptId = receiptId,
            UserId = userId,
            ProviderEventId = providerEventId,
            CalendarId = calendarId,
            Subject = subject,
            StartUtc = startUtc.HasValue ? DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc) : null,
            EndUtc = endUtc.HasValue ? DateTime.SpecifyKind(endUtc.Value, DateTimeKind.Utc) : null,
            IsCancelled = isCancelled,
            ChangeKey = changeKey,
            ICalUId = iCalUId,
            FetchedUtc = DateTime.SpecifyKind(fetchedUtc, DateTimeKind.Utc),
            FetchError = fetchError
        };
    }
}