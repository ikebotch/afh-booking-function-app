namespace AFH.Booking.Domain.Options;

public sealed class PartnerWorkflowOptions
{
    public const string SectionName = "PartnerWorkflow";

    public bool Enabled { get; set; } = false;
}
