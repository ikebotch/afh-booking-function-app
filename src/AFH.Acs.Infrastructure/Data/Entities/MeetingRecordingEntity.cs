using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;
public class MeetingRecordingEntity
{
    [Key]
    public string RecordingId { get; set; } = default!;
    public string MeetingId { get; set; } = default!;
    public string GroupId { get; set; } = default!;

    public string BlobName { get; set; } = default!;
    public string BlobUrl { get; set; } = default!;

    public DateTime RecordingStartUtc { get; set; }
    public DateTime? RecordingEndUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public MeetingEntity Meeting { get; set; } = default!;
}