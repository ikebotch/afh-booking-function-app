using System.Text.Json.Serialization;

public class AcsRecordingFileStatusData
{
    [JsonPropertyName("recordingStorageInfo")]
    public RecordingStorageInfo RecordingStorageInfo { get; set; }

    [JsonPropertyName("recordingStartTime")]
    public DateTimeOffset RecordingStartTime { get; set; }

    [JsonPropertyName("recordingDurationMs")]
    public long RecordingDurationMs { get; set; }

    [JsonPropertyName("recordingId")]
    public string RecordingId { get; set; }

    [JsonPropertyName("storageType")]
    public string StorageType { get; set; }

    [JsonPropertyName("sessionEndReason")]
    public string SessionEndReason { get; set; }
}

public class RecordingStorageInfo
{
    [JsonPropertyName("recordingChunks")]
    public List<RecordingChunk> RecordingChunks { get; set; }
}

public class RecordingChunk
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("endReason")]
    public string EndReason { get; set; }

    [JsonPropertyName("contentLocation")]
    public string ContentLocation { get; set; }

    [JsonPropertyName("metadataLocation")]
    public string MetadataLocation { get; set; }
}
