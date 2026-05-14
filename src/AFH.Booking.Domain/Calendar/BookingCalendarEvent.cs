namespace AFH.Booking.Domain.Calendar;

public sealed class BookingCalendarEvent
{
    private BookingCalendarEvent() { }

    public string UserId { get; private set; } = default!;
    public string ExternalId { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public string Timezone { get; private set; } = default!;
    public bool IsRemote { get; private set; }
    public IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();
    public string? Body { get; private set; }
    public string? EventId { get; private set; }
    public BookingShowAs ShowAs { get; private set; } = BookingShowAs.Busy;

    public CalendarLocation? Location { get; private set; }
    public IReadOnlyList<CalendarAttendee> Attendees { get; private set; } = Array.Empty<CalendarAttendee>();


    public static BookingCalendarEvent Update(
    string userId,
    BookingShowAs showAs,
    string? providerEventId,
    string? body,
      IEnumerable<string>? categories
    )
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("userId is required.");
        if (string.IsNullOrWhiteSpace(providerEventId)) throw new DomainException("userId is required.");
        var cats = categories?
     .Where(x => !string.IsNullOrWhiteSpace(x))
     .Select(x => x.Trim())
     .Distinct(StringComparer.OrdinalIgnoreCase)
     .ToArray() ?? Array.Empty<string>();
        return new BookingCalendarEvent
        {
            UserId = userId,
            EventId = providerEventId,
            ShowAs = showAs,
            Body = body,
            Categories = cats,

        };
    }



    public static BookingCalendarEvent Create(
        string userId,
        string externalId,
        string? subject,
        DateTime startUtc,
        DateTime endUtc,
        string timezone,
        bool isRemote,
        IEnumerable<string>? categories,
        string? body,
        string? providerEventId,
        CalendarLocation? location,
        IEnumerable<CalendarAttendee>? attendees,
        BookingShowAs showAs)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("userId is required.");
        if (string.IsNullOrWhiteSpace(externalId)) throw new DomainException("externalId is required.");
        if (string.IsNullOrWhiteSpace(timezone)) throw new DomainException("timezone is required.");
        if (endUtc <= startUtc) throw new DomainException("endUtc must be after startUtc.");

        var cats = categories?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        var att = attendees?.ToArray() ?? Array.Empty<CalendarAttendee>();

        return new BookingCalendarEvent
        {
            UserId = userId,
            ExternalId = externalId,
            Subject = string.IsNullOrWhiteSpace(subject) ? "AFH Booking" : subject!,
            StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            Timezone = timezone,
            IsRemote = isRemote,
            Categories = cats,
            Body = body,
            EventId = providerEventId,
            Location = location,
            Attendees = att,
            ShowAs = showAs
        };
    }
}

public sealed class CalendarAttendee
{
    private CalendarAttendee() { }

    public string Email { get; private set; } = default!;
    public string? Name { get; private set; }
    public bool IsRequired { get; private set; }

    public static CalendarAttendee Create(string email, string? name, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("attendee email is required.");

        return new CalendarAttendee
        {
            Email = email.Trim(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            IsRequired = isRequired
        };
    }
}

public sealed class CalendarLocation
{
    private CalendarLocation() { }

    public string DisplayName { get; private set; } = string.Empty;
    public string? AddressLine1 { get; private set; }
    public string? City { get; private set; }
    public string? Postcode { get; private set; }

    public static CalendarLocation Create(
        string? displayName,
        string? addressLine1,
        string? city,
        string? postcode)
    {
        if (string.IsNullOrWhiteSpace(displayName) &&
            string.IsNullOrWhiteSpace(addressLine1) &&
            string.IsNullOrWhiteSpace(city) &&
            string.IsNullOrWhiteSpace(postcode))
            throw new DomainException("location is required.");

        return new CalendarLocation
        {
            DisplayName = (displayName ?? string.Empty).Trim(),
            AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            Postcode = string.IsNullOrWhiteSpace(postcode) ? null : postcode.Trim()
        };
    }

    public static CalendarLocation? CreateOrNull(
        string? displayName,
        string? addressLine1,
        string? city,
        string? postcode)
    {
        // Convenience factory: return null if empty
        if (string.IsNullOrWhiteSpace(displayName) &&
            string.IsNullOrWhiteSpace(addressLine1) &&
            string.IsNullOrWhiteSpace(city) &&
            string.IsNullOrWhiteSpace(postcode))
            return null;

        return Create(displayName, addressLine1, city, postcode);
    }

    public bool IsEmpty()
        => string.IsNullOrWhiteSpace(DisplayName) &&
           string.IsNullOrWhiteSpace(AddressLine1) &&
           string.IsNullOrWhiteSpace(City) &&
           string.IsNullOrWhiteSpace(Postcode);
}
public enum BookingShowAs
{
    Free,
    Tentative,
    Busy,
    OutOfOffice
}