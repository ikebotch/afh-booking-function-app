namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class AdviserEntity
{
    public string AdviserId { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public ICollection<MeetingEntity> Meetings { get; set; } = new List<MeetingEntity>();
}
