using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class LeadEntity
{
    [Key]
    public string LeadId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string ClientName { get; set; } = default!;
    public string? ClientEmail { get; set; }
    public string SourceSystem { get; set; } = default!;  // e.g. Salesforce
    public string Status { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<MeetingEntity> Meetings { get; set; } = new List<MeetingEntity>();
}