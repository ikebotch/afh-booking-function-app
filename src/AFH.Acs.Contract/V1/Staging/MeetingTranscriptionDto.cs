namespace AFH.Acs.Recorder.DTOs;


public class MeetingTranscriptionDto
{
    public string TranscriptionId { get; set; } = default!;
    public string Language { get; set; } = "en-GB";
    public string FullText { get; set; } = default!;
    public string? SummaryText { get; set; }

}
public class RecordingTranscriptionRequest
{
    public string ClientName { get; set; }
    public DateTime MeetingDate { get; set; }
    public string ClientEntityID { get; set; } = default!;
    public string Filename { get; set; }
    public string AttitudetoRisk { get; set; }
    public string FinancialGoals { get; set; }
    public string Tax { get; set; }

  
    public string MeetingType { get; set; }
    public string NotesStatus { get; set; }
    public string AdviserEmail { get; set; }
    public string ContentType { get; set; }


    public string RecordingId { get; set; } = default!;
    public string MeetingId { get; set; } = default!;
    public string GroupId { get; set; } = default!;
    public string BlobName { get; set; } = default!;
    public string BlobUrl { get; set; } = default!;

    public string? Transcription { get; set; } = default!;

    // non-SAS URL
    //public DateTime Modified { get; set; }
    //public DateTime Created { get; set; }
    //public int AuthorLookupId { get; set; }
    //public int EditorLookupId { get; set; }



    //public string ODataEtag { get; set; }
    //public string UIVersionString { get; set; }
    //public string Attachments { get; set; }
    //public string Edit { get; set; }
    //public int ItemChildCount { get; set; }
    //public int FolderChildCount { get; set; }
    //public string ComplianceFlags { get; set; }
    //public string ComplianceTag { get; set; }
    //public DateTime ComplianceTagWrittenTime { get; set; }
    //public int ComplianceTagUserId { get; set; }
    //public int AppAuthorLookupId { get; set; }
    //public int AppEditorLookupId { get; set; }

}