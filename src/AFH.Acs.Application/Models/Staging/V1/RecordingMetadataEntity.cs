using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Models.V1;
public record CreateMeetingResponse(string GroupId, string JoinUrl);
public record IssueTokenResponse(string IdentityId, string Token, DateTimeOffset ExpiresOn);

public class RecordingMetadataEntity : ITableEntity
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

