namespace AFH.Booking.Domain.Options;

public sealed class OutlookGovernanceOptions
{
    public const string SectionName = "OutlookGovernance";

    public bool Enabled { get; set; } = true;
    public int EscalationThreshold { get; set; } = 3;
    public int EscalationWindowHours { get; set; } = 24;
    public bool AutoReconcileDeletedEvents { get; set; }
    public bool AdviserNotificationsEnabled { get; set; } = true;
    public string[] ManagerRecipients { get; set; } = [];
}
