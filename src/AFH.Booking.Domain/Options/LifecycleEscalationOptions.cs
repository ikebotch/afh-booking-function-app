namespace AFH.Booking.Domain.Options;

public sealed class LifecycleEscalationOptions
{
    public const string SectionName = "Lifecycle:Escalation";

    public bool Enabled { get; set; }
    public string? QueueName { get; set; }
    public string? Owner { get; set; }
}
