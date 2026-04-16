using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Data.Tables;

namespace AFH.Acs.Recorder.Services;

public class MeetingsFunction : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // serverCallId or groupId
    public string RowKey { get; set; } = default!;       // recordingId-<guid>
    public string? BlobUrl { get; set; }
    public string? ContentLocation { get; set; }
    public string? MetadataLocation { get; set; }
    public DateTimeOffset? RecordingStartUtc { get; set; }
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}
