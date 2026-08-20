namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class PartnerWorkflowRuleModel
{
    public string ChangeType { get; set; } = default!;
    public string PartnerKey { get; set; } = "Default";
    public bool Enabled { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public PartnerWorkflowEndpointModel? Endpoint { get; set; }
}
