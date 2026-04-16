using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Models.V1;

public class StartRecordingRequest
{
    public string GroupId { get; set; } = default!;
    public string? MeetingId { get; set; }
}

public class StartRecordingResult
{
    public string RecordingId { get; set; } = default!;
    public string GroupId { get; set; } = default!;
    public string? MeetingId { get; set; }
}

public class StopRecordingRequest
{
    public string RecordingId { get; set; } = default!;
}



public record RecordingListItem(
    string Id,
    string Name,
    string Url,
    DateTimeOffset? LastModified,
    long? SizeBytes);

public record RecordingDownloadInfo(
    string DownloadUri,
    string BlobName,
    DateTimeOffset? LastModified);


public record IssueTokenResult(
    string IdentityId,
    string Token,
    DateTimeOffset ExpiresOn);