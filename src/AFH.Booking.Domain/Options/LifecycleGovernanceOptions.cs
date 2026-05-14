namespace AFH.Booking.Domain.Options;

public sealed class LifecycleGovernanceOptions
{
    public const string SectionName = "Lifecycle:Governance";

    public string? PolicyName { get; set; }
    public string? ReviewOwner { get; set; }
    public int RetentionDays { get; set; } = 90;
}
