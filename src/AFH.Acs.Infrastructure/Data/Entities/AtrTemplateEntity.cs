using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;
public class AtrTemplateEntity
{
    [Key]
    public string AtrId { get; set; } = default!;
    public string RiskLevel { get; set; } = default!;       // CAUTIOUS / BALANCED / ADVENTUROUS
    public string ParagraphText { get; set; } = default!;
    public string KeypointHeader { get; set; } = default!;  // CAPACITY_FOR_LOSS, etc.
    public DateTime CreatedAtUtc { get; set; }
}
