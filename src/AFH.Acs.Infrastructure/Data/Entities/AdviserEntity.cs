using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class AdviserEntity
{
    [Key]
    public string AdviserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Region { get; set; } = default!;
    public bool LeadTechFlag { get; set; }
    public bool ActiveFlag { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<MeetingEntity> Meetings { get; set; } = new List<MeetingEntity>();
}
