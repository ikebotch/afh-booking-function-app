namespace AFH.Acs.Recorder.DTOs;

public class MeetingScheduleRequest
{
    /// <summary>
    /// Adviser who will host the meeting.
    /// </summary>
    public string AdviserId { get; set; } = default!;

    /// <summary>
    /// Lead (from Snowflake) the meeting is attached to.
    /// </summary>
    public string LeadId { get; set; } = default!;

    /// <summary>
    /// Meeting type, e.g. "Review", "Initial", etc.
    /// </summary>
    public string MeetingType { get; set; } = default!;

    /// <summary>
    /// Meeting title shown in the calendar & join page.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Optional description/body for the calendar event.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// UTC start time of the meeting.
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// UTC end time of the meeting.
    /// </summary>
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Email of the client. Can be taken from Lead but overrideable.
    /// </summary>
    public string ClientEmail { get; set; } = default!;

    /// <summary>
    /// Optional name of the client (also available via Lead).
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Optional location or channel, e.g. "Online", "Office", etc.
    /// </summary>
    public string? Location { get; set; }
}

public class MeetingScheduleResponse
{
    public string MeetingId { get; set; } = default!;
    public string GroupId { get; set; } = default!;

    /// <summary>
    /// Graph event ID of the adviser’s calendar entry.
    /// </summary>
    public string GraphEventId { get; set; } = default!;

    /// <summary>
    /// URL for the client to join (usually embedded in email).
    /// </summary>
    public string ClientJoinUrl { get; set; } = default!;

    /// <summary>
    /// URL for the adviser to join (from calendar / internal app).
    /// </summary>
    public string AdviserJoinUrl { get; set; } = default!;

    /// <summary>
    /// The join code that maps to GroupId (for the /meeting/{code} frontend).
    /// </summary>
    public string JoinCode { get; set; } = default!;

    public string AdviserId { get; set; } = default!;
    public string LeadId { get; set; } = default!;
    public string MeetingType { get; set; } = default!;
    public string Title { get; set; } = default!;

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string ClientEmail { get; set; } = default!;
    public string? ClientName { get; set; }
}



//public class MeetingDetailsDto
//{
//    public string MeetingId { get; set; } = default!;
//    public string GroupId { get; set; } = default!;

//    public string AdviserId { get; set; } = default!;
//    public string? AdviserName { get; set; }

//    public string LeadId { get; set; } = default!;
//    public string MeetingType { get; set; } = default!;
//    public string Title { get; set; } = default!;

//    public DateTimeOffset Start { get; set; }
//    public DateTimeOffset End { get; set; }

//    public string ClientEmail { get; set; } = default!;
//    public string? ClientName { get; set; }

//    public bool ConsentToRecording { get; set; }
//    public DateTimeOffset? ConsentTimestampUtc { get; set; }

//    public string Status { get; set; } = "Scheduled";

//    public List<MeetingAttendeeDto> Attendees { get; set; } = new();
//    public List<MeetingRecordingDto> Recordings { get; set; } = new();
//    public MeetingTranscriptionDto? Transcription { get; set; }
//}

//public class MeetingAttendeeDto
//{
//    public string Email { get; set; } = default!;
//    public string Role { get; set; } = default!;            // Adviser / Client / Other
//    public string ResponseStatus { get; set; } = "None";    // Accepted / Declined / Tentative / None
//    public DateTimeOffset? ResponseTimeUtc { get; set; }
//}

//public class MeetingRecordingDto
//{
//    public string RecordingId { get; set; } = default!;
//    public string BlobName { get; set; } = default!;
//    public string BlobUrl { get; set; } = default!;

//    public DateTimeOffset RecordingStartUtc { get; set; }
//    public DateTimeOffset RecordingEndUtc { get; set; }
//    public int? DurationSeconds { get; set; }
//}

//public class MeetingTranscriptionDto
//{
//    public string TranscriptionId { get; set; } = default!;
//    public string Language { get; set; } = "en-GB";

//    public string FullText { get; set; } = default!;
//    public string? SummaryText { get; set; }
//}


//public class MeetingConsentRequest
//{
//    /// <summary>
//    /// True if the client consents to recording, otherwise false.
//    /// </summary>
//    public bool Consent { get; set; }
//}

//public class MeetingConsentResponse
//{
//    public string MeetingId { get; set; } = default!;
//    public string GroupId { get; set; } = default!;

//    public bool ConsentToRecording { get; set; }
//    public DateTimeOffset ConsentTimestampUtc { get; set; }
//}



//public class JoinTokenRequest
//{
//    /// <summary>
//    /// Display name to show in the ACS call.
//    /// </summary>
//    public string DisplayName { get; set; } = default!;

//    /// <summary>
//    /// Role of the joining user, e.g. "Client" or "Adviser".
//    /// </summary>
//    public string Role { get; set; } = "Client";
//}

//public class JoinTokenResponse
//{
//    public string UserId { get; set; } = default!;
//    public string Token { get; set; } = default!;
//    public DateTimeOffset ExpiresOn { get; set; }
//}
