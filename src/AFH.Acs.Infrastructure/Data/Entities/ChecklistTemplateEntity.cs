using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class ChecklistTemplateEntity
{
    [Key]
    public string TemplateId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string MeetingType { get; set; } = default!;
    public bool ActiveFlag { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<ChecklistItemTemplateEntity> Items { get; set; } = new List<ChecklistItemTemplateEntity>();
}