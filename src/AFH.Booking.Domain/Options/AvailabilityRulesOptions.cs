namespace AFH.Booking.Domain.Options;

public sealed class AvailabilityRulesOptions
{
    public const string SectionName = "AvailabilityRules";

    public int MinimumAppointmentMinutes { get; set; } = 1;
    public string DefaultWorkingDayStart { get; set; } = "08:00";
    public string DefaultWorkingDayEnd { get; set; } = "17:00";
    public int CapacityWindowDays { get; set; } = 1;
    public List<AdviserWorkingPatternOptions> WorkingPatterns { get; set; } = [];
    public List<AdviserCapacityOptions> CapacityLimits { get; set; } = [];
}

public sealed class AdviserWorkingPatternOptions
{
    public string AdviserId { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
}

public sealed class AdviserCapacityOptions
{
    public string AdviserId { get; set; } = string.Empty;
    public int MaxActiveBookings { get; set; }
}
