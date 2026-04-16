using System.Text.Json;

namespace AFH.Acs.Function.Functions.V1.System;

public static class OpenApiDocumentFactory
{
    public static string CreateJson()
    {
        var document = new
        {
            openapi = "3.0.1",
            info = new
            {
                title = "AFH ACS Function API",
                version = "v1",
                description = "Meeting orchestration, media, and transcription."
            },
            servers = new[]
            {
                new { url = "/api", description = "Azure Functions base path" }
            },
            paths = new Dictionary<string, object>
            {
                ["/v1/health"] = new
                {
                    get = Operation(
                        "Get service health",
                        "health",
                        "Returns a simple liveness response.",
                        "System",
                        Array.Empty<object>(),
                        Response("Service is healthy"))
                },
                ["/v1/meet/create"] = new
                {
                    post = Operation(
                        "Create meeting",
                        "createMeeting",
                        "Creates a meeting session and returns join URLs.",
                        "Meetings",
                        Array.Empty<object>(),
                        Response("Meeting created", "#/components/schemas/MeetingScheduleResponse"),
                        RequestBody("#/components/schemas/ScheduleMeetingRequest"))
                },
                ["/v1/meet/identity-token"] = new
                {
                    post = Operation(
                        "Issue meeting identity token",
                        "issueMeetingIdentityToken",
                        "Issues a meeting identity and access token.",
                        "Meetings",
                        Array.Empty<object>(),
                        Response("Identity token", "#/components/schemas/IdentityTokenResponse"))
                },
                ["/v1/meet/link"] = new
                {
                    post = Operation(
                        "Create meeting link",
                        "createMeetingLink",
                        "Creates a meeting join link from a booking identifier.",
                        "Meetings",
                        Array.Empty<object>(),
                        Response("Meeting link", "#/components/schemas/MeetingLinkResponse"),
                        RequestBody("#/components/schemas/CreateMeetingLinkRequest"))
                },
                ["/v1/meet/{groupId}/join-token"] = new
                {
                    post = Operation(
                        "Issue join token",
                        "issueJoinToken",
                        "Issues a join token for a meeting group.",
                        "Meetings",
                        [Parameter("groupId", "path", true, "string", "Meeting group identifier.")],
                        Response("Join token", "#/components/schemas/JoinTokenResponse"),
                        RequestBody("#/components/schemas/JoinTokenRequest"))
                },
                ["/v1/meet/{groupId}/consent"] = new
                {
                    post = Operation(
                        "Record recording consent",
                        "recordConsent",
                        "Updates recording consent for a meeting group.",
                        "Meetings",
                        [Parameter("groupId", "path", true, "string", "Meeting group identifier.")],
                        Response("Consent recorded", "#/components/schemas/MeetingConsentResponse"),
                        RequestBody("#/components/schemas/MeetingConsentRequest"))
                },
                ["/v1/meetings/{meetingId}"] = new
                {
                    get = Operation(
                        "Get meeting by id",
                        "getMeetingById",
                        "Returns the meeting session by meeting identifier.",
                        "Meetings",
                        [Parameter("meetingId", "path", true, "string", "Meeting identifier.")],
                        Response("Meeting details", "#/components/schemas/MeetingDetailsResponse"))
                },
                ["/v1/meet/{groupId}"] = new
                {
                    get = Operation(
                        "Get meeting by group",
                        "getMeetingByGroup",
                        "Returns the meeting session by group identifier.",
                        "Meetings",
                        [Parameter("groupId", "path", true, "string", "Meeting group identifier.")],
                        Response("Meeting details", "#/components/schemas/MeetingDetailsResponse"))
                },
                ["/v1/recordings/start"] = new
                {
                    post = Operation(
                        "Start recording",
                        "startRecording",
                        "Starts or resumes a meeting recording.",
                        "Recordings",
                        Array.Empty<object>(),
                        Response("Recording started", "#/components/schemas/MeetingRecordingResponse"),
                        RequestBody("#/components/schemas/StartRecordingRequest"))
                },
                ["/v1/recordings/stop"] = new
                {
                    post = Operation(
                        "Stop recording",
                        "stopRecording",
                        "Stops an active meeting recording.",
                        "Recordings",
                        Array.Empty<object>(),
                        Response("Recording stopped", "#/components/schemas/MeetingRecordingResponse"),
                        RequestBody("#/components/schemas/StopRecordingRequest"))
                },
                ["/v1/recordings"] = new
                {
                    get = Operation(
                        "List recordings",
                        "listRecordings",
                        "Lists meeting recordings, optionally filtered by meeting identifier.",
                        "Recordings",
                        [Parameter("meetingId", "query", false, "string", "Optional meeting identifier.")],
                        Response("Recording list", "#/components/schemas/RecordingListResponse"))
                },
                ["/v1/recordings/{recordingId}"] = new
                {
                    get = Operation(
                        "Get recording",
                        "getRecording",
                        "Returns a recording by identifier.",
                        "Recordings",
                        [Parameter("recordingId", "path", true, "string", "Recording identifier.")],
                        Response("Recording", "#/components/schemas/MeetingRecordingResponse"))
                },
                ["/v1/meetings/{meetingId}/transcriptions"] = new
                {
                    post = Operation(
                        "Submit meeting transcription job",
                        "submitMeetingTranscription",
                        "Submits a meeting recording URL to Speech AI for transcription.",
                        "Transcription",
                        [Parameter("meetingId", "path", true, "string", "Meeting identifier.")],
                        Response("Transcription job submitted", "#/components/schemas/TranscriptionJobResponse"),
                        RequestBody("#/components/schemas/SubmitTranscriptionRequest"))
                },
                ["/v1/transcriptions/{jobId}"] = new
                {
                    get = Operation(
                        "Get transcription job status",
                        "getTranscriptionStatus",
                        "Returns the current status of a transcription job.",
                        "Transcription",
                        [Parameter("jobId", "path", true, "string", "Transcription job identifier.")],
                        Response("Job status", "#/components/schemas/TranscriptionJobResponse")),
                    delete = Operation(
                        "Delete transcription job",
                        "deleteTranscriptionJob",
                        "Deletes a transcription job from Speech AI.",
                        "Transcription",
                        [Parameter("jobId", "path", true, "string", "Transcription job identifier.")],
                        Response("Transcription job deleted"))
                },
                ["/v1/transcriptions/{jobId}/files"] = new
                {
                    get = Operation(
                        "List transcription files",
                        "listTranscriptionFiles",
                        "Lists the files returned for a transcription job.",
                        "Transcription",
                        [Parameter("jobId", "path", true, "string", "Transcription job identifier.")],
                        Response("Job files", "#/components/schemas/TranscriptionFilesResponse"))
                },
                ["/v1/transcriptions/{jobId}/content"] = new
                {
                    get = Operation(
                        "Get transcript content",
                        "getTranscriptionContent",
                        "Returns the transcript text and speaker-formatted text for a job.",
                        "Transcription",
                        [Parameter("jobId", "path", true, "string", "Transcription job identifier.")],
                        Response("Transcript content", "#/components/schemas/TranscriptionContentResponse"))
                },
                ["/v1/transcriptions/{jobId}/speaker-content"] = new
                {
                    get = Operation(
                        "Get speaker-formatted transcript",
                        "getSpeakerFormattedTranscript",
                        "Returns the speaker-formatted transcript as plain text.",
                        "Transcription",
                        [Parameter("jobId", "path", true, "string", "Transcription job identifier.")],
                        PlainTextResponse("Speaker-formatted transcript"))
                },
                ["/v1/transcriptions/{jobId}/cancel"] = new
                {
                    post = Operation(
                        "Cancel transcription job",
                        "cancelTranscriptionJob",
                        "Cancels a transcription job.",
                        "Transcription",
                        [Parameter("jobId", "path", true, "string", "Transcription job identifier.")],
                        Response("Transcription job cancelled"))
                },
                ["/v1/scalar"] = new
                {
                    get = Operation(
                        "Get Scalar UI",
                        "scalarUi",
                        "Returns a minimal scalar shell for the retained ACS endpoints.",
                        "System",
                        Array.Empty<object>(),
                        HtmlResponse("Scalar UI"))
                },
                ["/v1/openapi.json"] = new
                {
                    get = Operation(
                        "Get OpenAPI document",
                        "openApiJson",
                        "Returns the OpenAPI JSON document for the retained ACS endpoints.",
                        "System",
                        Array.Empty<object>(),
                        JsonResponse("OpenAPI JSON"))
                }
            },
            components = new
            {
                schemas = new Dictionary<string, object>
                {
                    ["ScheduleMeetingRequest"] = new
                    {
                        type = "object",
                        required = new[] { "adviserId", "leadId", "meetingType", "title", "start", "end", "clientEmail" },
                        properties = new Dictionary<string, object>
                        {
                            ["adviserId"] = new { type = "string" },
                            ["leadId"] = new { type = "string" },
                            ["meetingType"] = new { type = "string" },
                            ["title"] = new { type = "string" },
                            ["description"] = new { type = "string", nullable = true },
                            ["start"] = new { type = "string", format = "date-time" },
                            ["end"] = new { type = "string", format = "date-time" },
                            ["clientEmail"] = new { type = "string", format = "email" },
                            ["clientName"] = new { type = "string", nullable = true },
                            ["location"] = new { type = "string", nullable = true }
                        }
                    },
                    ["MeetingScheduleResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string" },
                            ["groupId"] = new { type = "string" },
                            ["joinCode"] = new { type = "string" },
                            ["clientJoinUrl"] = new { type = "string", format = "uri" },
                            ["adviserJoinUrl"] = new { type = "string", format = "uri" },
                            ["adviserId"] = new { type = "string" },
                            ["leadId"] = new { type = "string" },
                            ["meetingType"] = new { type = "string" },
                            ["title"] = new { type = "string" },
                            ["start"] = new { type = "string", format = "date-time" },
                            ["end"] = new { type = "string", format = "date-time" },
                            ["clientEmail"] = new { type = "string", format = "email" },
                            ["clientName"] = new { type = "string", nullable = true }
                        }
                    },
                    ["CreateMeetingLinkRequest"] = new
                    {
                        type = "object",
                        required = new[] { "bookingId" },
                        properties = new Dictionary<string, object>
                        {
                            ["bookingId"] = new { type = "string" }
                        }
                    },
                    ["MeetingLinkResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["bookingId"] = new { type = "string" },
                            ["groupId"] = new { type = "string" },
                            ["joinCode"] = new { type = "string" },
                            ["joinUrl"] = new { type = "string", format = "uri" }
                        }
                    },
                    ["IdentityTokenResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["identityId"] = new { type = "string" },
                            ["token"] = new { type = "string" },
                            ["expiresOn"] = new { type = "string", format = "date-time" }
                        }
                    },
                    ["JoinTokenRequest"] = new
                    {
                        type = "object",
                        required = new[] { "displayName" },
                        properties = new Dictionary<string, object>
                        {
                            ["displayName"] = new { type = "string" },
                            ["role"] = new { type = "string", nullable = true }
                        }
                    },
                    ["JoinTokenResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string" },
                            ["groupId"] = new { type = "string" },
                            ["userId"] = new { type = "string" },
                            ["token"] = new { type = "string" },
                            ["expiresOn"] = new { type = "string", format = "date-time" },
                            ["displayName"] = new { type = "string", nullable = true }
                        }
                    },
                    ["MeetingConsentRequest"] = new
                    {
                        type = "object",
                        required = new[] { "consent" },
                        properties = new Dictionary<string, object>
                        {
                            ["consent"] = new { type = "boolean" }
                        }
                    },
                    ["MeetingConsentResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string" },
                            ["groupId"] = new { type = "string" },
                            ["consentToRecording"] = new { type = "boolean" },
                            ["consentTimestampUtc"] = new { type = "string", format = "date-time" }
                        }
                    },
                    ["MeetingRecordingResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["recordingId"] = new { type = "string" },
                            ["meetingId"] = new { type = "string" },
                            ["groupId"] = new { type = "string" },
                            ["blobName"] = new { type = "string" },
                            ["blobUrl"] = new { type = "string", format = "uri" },
                            ["recordingStartUtc"] = new { type = "string", format = "date-time" },
                            ["recordingEndUtc"] = new { type = "string", format = "date-time", nullable = true },
                            ["durationSeconds"] = new { type = "integer", format = "int32", nullable = true }
                        }
                    },
                    ["RecordingListResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string", nullable = true },
                            ["items"] = new
                            {
                                type = "array",
                                items = new Dictionary<string, object>
                                {
                                    ["$ref"] = "#/components/schemas/MeetingRecordingResponse"
                                }
                            }
                        }
                    },
                    ["MeetingTranscriptionResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["transcriptionId"] = new { type = "string" },
                            ["language"] = new { type = "string" },
                            ["fullText"] = new { type = "string" },
                            ["summaryText"] = new { type = "string", nullable = true }
                        }
                    },
                    ["MeetingAttendeeResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["email"] = new { type = "string" },
                            ["role"] = new { type = "string" },
                            ["responseStatus"] = new { type = "string" },
                            ["responseTimeUtc"] = new { type = "string", format = "date-time", nullable = true }
                        }
                    },
                    ["MeetingDetailsResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string" },
                            ["groupId"] = new { type = "string" },
                            ["adviserId"] = new { type = "string" },
                            ["adviserName"] = new { type = "string", nullable = true },
                            ["leadId"] = new { type = "string" },
                            ["meetingType"] = new { type = "string" },
                            ["title"] = new { type = "string" },
                            ["start"] = new { type = "string", format = "date-time" },
                            ["end"] = new { type = "string", format = "date-time" },
                            ["clientEmail"] = new { type = "string" },
                            ["clientName"] = new { type = "string", nullable = true },
                            ["consentToRecording"] = new { type = "boolean" },
                            ["consentTimestampUtc"] = new { type = "string", format = "date-time", nullable = true },
                            ["status"] = new { type = "string" },
                            ["attendees"] = new
                            {
                                type = "array",
                                items = new Dictionary<string, object>
                                {
                                    ["$ref"] = "#/components/schemas/MeetingAttendeeResponse"
                                }
                            },
                            ["recordings"] = new
                            {
                                type = "array",
                                items = new Dictionary<string, object>
                                {
                                    ["$ref"] = "#/components/schemas/MeetingRecordingResponse"
                                }
                            },
                            ["transcription"] = new Dictionary<string, object>
                            {
                                ["$ref"] = "#/components/schemas/MeetingTranscriptionResponse",
                                ["nullable"] = true
                            }
                        }
                    },
                    ["StartRecordingRequest"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string", nullable = true },
                            ["groupId"] = new { type = "string", nullable = true },
                            ["blobName"] = new { type = "string", nullable = true }
                        }
                    },
                    ["StopRecordingRequest"] = new
                    {
                        type = "object",
                        required = new[] { "recordingId" },
                        properties = new Dictionary<string, object>
                        {
                            ["recordingId"] = new { type = "string" }
                        }
                    },
                    ["SubmitTranscriptionRequest"] = new
                    {
                        type = "object",
                        required = new[] { "contentUrl" },
                        properties = new Dictionary<string, object>
                        {
                            ["contentUrl"] = new { type = "string", format = "uri" },
                            ["displayName"] = new { type = "string", nullable = true },
                            ["locale"] = new { type = "string", nullable = true },
                            ["settings"] = new
                            {
                                type = "object",
                                nullable = true,
                                properties = new Dictionary<string, object>
                                {
                                    ["diarizationEnabled"] = new { type = "boolean", nullable = true },
                                    ["wordLevelTimestampsEnabled"] = new { type = "boolean", nullable = true }
                                }
                            }
                        }
                    },
                    ["TranscriptionJobResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["meetingId"] = new { type = "string", nullable = true },
                            ["jobId"] = new { type = "string" },
                            ["status"] = new { type = "string", nullable = true },
                            ["displayName"] = new { type = "string", nullable = true },
                            ["createdDateTime"] = new { type = "string", format = "date-time", nullable = true },
                            ["lastActionDateTime"] = new { type = "string", format = "date-time", nullable = true },
                            ["locale"] = new { type = "string", nullable = true },
                            ["model"] = new { type = "string", nullable = true },
                            ["sourceUrl"] = new { type = "string", format = "uri", nullable = true }
                        }
                    },
                    ["TranscriptionFileResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["name"] = new { type = "string" },
                            ["kind"] = new { type = "string", nullable = true },
                            ["createdDateTime"] = new { type = "string", format = "date-time", nullable = true },
                            ["sizeInBytes"] = new { type = "integer", format = "int64", nullable = true },
                            ["contentLength"] = new { type = "integer", format = "int64", nullable = true },
                            ["self"] = new { type = "string", format = "uri", nullable = true },
                            ["contentUrl"] = new { type = "string", format = "uri", nullable = true },
                            ["contentUri"] = new { type = "string", format = "uri", nullable = true }
                        }
                    },
                    ["TranscriptionFilesResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["jobId"] = new { type = "string" },
                            ["primaryTranscriptFile"] = new Dictionary<string, object>
                            {
                                ["$ref"] = "#/components/schemas/TranscriptionFileResponse",
                                ["nullable"] = true
                            },
                            ["files"] = new
                            {
                                type = "array",
                                items = new Dictionary<string, object>
                                {
                                    ["$ref"] = "#/components/schemas/TranscriptionFileResponse"
                                }
                            }
                        }
                    },
                    ["TranscriptionContentResponse"] = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["jobId"] = new { type = "string" },
                            ["transcriptFileName"] = new { type = "string", nullable = true },
                            ["transcriptFileUrl"] = new { type = "string", format = "uri", nullable = true },
                            ["transcriptText"] = new { type = "string", nullable = true },
                            ["speakerFormattedTranscript"] = new { type = "string", nullable = true }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static object Operation(
        string summary,
        string operationId,
        string description,
        string tag,
        object[] parameters,
        object response,
        object? requestBody = null)
    {
        var responses = new Dictionary<string, object>();

        if (operationId is "deleteTranscriptionJob")
        {
            responses["204"] = new { description = "No content" };
        }
        else
        {
            responses["200"] = response;
        }

        if (operationId is "createMeeting" or "createMeetingLink" or "startRecording" or "stopRecording" or "submitMeetingTranscription")
        {
            responses["400"] = new { description = "Invalid request" };
        }

        if (operationId is "getMeetingById" or "getMeetingByGroup" or "getRecording")
        {
            responses["404"] = new { description = "Not found" };
        }

        return new Dictionary<string, object?>
        {
            ["summary"] = summary,
            ["operationId"] = operationId,
            ["description"] = description,
            ["parameters"] = parameters,
            ["responses"] = responses,
            ["requestBody"] = requestBody,
            ["tags"] = new[] { tag }
        };
    }

    private static object Parameter(string name, string location, bool required, string type, string description)
        => new
        {
            name,
            @in = location,
            required,
            description,
            schema = new { type }
        };

    private static object RequestBody(string schemaRef)
        => new
        {
            required = true,
            content = new Dictionary<string, object>
            {
                ["application/json"] = new
                {
                    schema = new Dictionary<string, object>
                    {
                        ["$ref"] = schemaRef
                    }
                }
            }
        };

    private static object Response(string description, string? schemaRef = null)
        => schemaRef is null
            ? new { description }
            : new
            {
                description,
                content = new Dictionary<string, object>
                {
                    ["application/json"] = new
                    {
                        schema = new Dictionary<string, object>
                        {
                            ["$ref"] = schemaRef
                        }
                    }
                }
            };

    private static object PlainTextResponse(string description)
        => new
        {
            description,
            content = new Dictionary<string, object>
            {
                ["text/plain"] = new
                {
                    schema = new { type = "string" }
                }
            }
        };

    private static object JsonResponse(string description)
        => new
        {
            description,
            content = new Dictionary<string, object>
            {
                ["application/json"] = new
                {
                    schema = new { type = "string" }
                }
            }
        };

    private static object HtmlResponse(string description)
        => new
        {
            description,
            content = new Dictionary<string, object>
            {
                ["text/html"] = new
                {
                    schema = new { type = "string" }
                }
            }
        };
}
