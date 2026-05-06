namespace AFH.Booking.Domain.Options;

public sealed class BookingConfigOptions
{
    public const string SectionName = "BookingConfig";

    public List<MeetingTypeOptions> MeetingTypes { get; set; } =
    [
        new() { Code = "Initial", Label = "Initial", IsDefault = true },
        new() { Code = "Review", Label = "Review" },
        new() { Code = "Wills", Label = "Wills" }
    ];
}

public sealed class MeetingTypeOptions
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int? DefaultDurationMinutes { get; set; }
}
