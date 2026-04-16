namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class LeadEntity
{
    public string LeadId { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public ICollection<MeetingEntity> Meetings { get; set; } = new List<MeetingEntity>();
}
