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

    public List<MeetingTopicOptions> MeetingTopics { get; set; } =
    [
        new() { Code = "Retirement", Label = "Retirement" },
        new() { Code = "Pension", Label = "Pension" },
        new() { Code = "Will", Label = "Will" }
    ];
}

public sealed class MeetingTypeOptions
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int? DefaultDurationMinutes { get; set; }
}

public sealed class MeetingTopicOptions
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
