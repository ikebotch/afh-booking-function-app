using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Helpers;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using Azure;
using Azure.Communication.CallAutomation;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Recorder.Services;

public class RecordingService : IRecordingService
{
    private readonly CallAutomationClient _callAutomation;
    private readonly IConfiguration _config;
    private readonly ILogger<RecordingService> _logger;
    private readonly IRecordingRepository _recordings;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    public RecordingService(
        CallAutomationClient callAutomation,
        IConfiguration config,
        ILogger<RecordingService> logger,
        IRecordingRepository recordings,
        BlobServiceClient blobServiceClient)
    {
        _callAutomation = callAutomation;
        _config = config;
        _logger = logger;
        _recordings = recordings;
        _blobServiceClient = blobServiceClient;
        _containerName = _config["FunctionApp:AudioFileContainerName"] ?? "recordings";

    }

    public async Task<StartRecordingResult> StartAsync(
        StartRecordingRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.GroupId))
            throw new ArgumentException("GroupId is required.", nameof(request.GroupId));

        if (request.MeetingId == null)
            throw new ArgumentException("MeetingId is required.", nameof(request.MeetingId));

        var groupId = request.GroupId;

        // 1) Check if we already have an active recording for this group
        var existing = await _recordings.GetActiveByGroupIdAsync(groupId, ct);
        if (existing != null)
        {
            _logger.LogInformation(
                "Recording already active for GroupId={GroupId}. Reusing RecordingId={RecordingId}",
                groupId,
                existing.RecordingId);

            return new StartRecordingResult
            {
                RecordingId = existing.RecordingId,
                GroupId = existing.GroupId,
                MeetingId = existing.MeetingId
            };
        }

        // 2) Storage settings
        var audioStorageConn = _config["AzureWebJobsAudioFileStorage"]
                               ?? throw new InvalidOperationException("AzureWebJobsAudioFileStorage not configured.");
        var containerName = _config["FunctionApp:AudioFileContainerName"] ?? "recordings";

        var blobSvc = new BlobServiceClient(audioStorageConn);
        var accountName = blobSvc.AccountName;
        var containerUrl = new Uri($"https://{accountName}.blob.core.windows.net/{containerName}");

        _logger.LogInformation(
            "Starting recording for GroupId={GroupId}, MeetingId={MeetingId}, ContainerUrl={ContainerUrl}",
            groupId, request.MeetingId, containerUrl);

        var locator = new GroupCallLocator(groupId);

        var opts = new StartRecordingOptions(locator)
        {
            RecordingContent = RecordingContent.Audio,
            RecordingChannel = RecordingChannel.Mixed,
            RecordingFormat = RecordingFormat.Wav,
            RecordingStorage = RecordingStorage.CreateAzureBlobContainerRecordingStorage(containerUrl)
        };

        try
        {
            // 3) Ask ACS to start recording
            var started = await _callAutomation.GetCallRecording().StartAsync(opts, ct);
            var recordingId = started.Value.RecordingId;
            var nowUtc = DateTime.UtcNow;

            _logger.LogInformation(
                "Recording started. GroupId={GroupId}, MeetingId={MeetingId}, RecordingId={RecordingId}",
                groupId, request.MeetingId, recordingId);

            // Persist MEETING_RECORDING row
            var entity = new MeetingRecordingEntity
            {
                RecordingId = recordingId,
                MeetingId = request.MeetingId,
                GroupId = groupId,

                BlobName = string.Empty,
                BlobUrl = containerUrl.ToString(),

                RecordingStartUtc = nowUtc,
                //RecordingEndUtc = null,   // <== keep null while active
                DurationSeconds = null,
                CreatedAtUtc = nowUtc
            };

            await _recordings.AddAsync(entity, ct);

            return new StartRecordingResult
            {
                RecordingId = recordingId,
                GroupId = groupId,
                MeetingId = request.MeetingId
            };
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "8559")
        {
            // Duplicate start – recording already started / in progress
            _logger.LogWarning(
                ex,
                "Duplicate start recording request for GroupId={GroupId}. Recording already in progress.",
                groupId);

            // Try to fetch the active recording again (in case another instance created it)
            var active = await _recordings.GetActiveByGroupIdAsync(groupId, ct);
            if (active != null)
            {
                _logger.LogInformation(
                    "Returning existing active recording after 8559. GroupId={GroupId}, RecordingId={RecordingId}",
                    groupId,
                    active.RecordingId);

                return new StartRecordingResult
                {
                    RecordingId = active.RecordingId,
                    GroupId = active.GroupId,
                    MeetingId = active.MeetingId
                };
            }

            // Fallback – we know ACS is already recording, but we don't have a row.
            // You could choose to rethrow, or create a minimal row without blob info.
            throw;
        }
    }

    public async Task StopAsync(
        StopRecordingRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.RecordingId))
            throw new ArgumentException("RecordingId is required.", nameof(request.RecordingId));

        _logger.LogInformation("Stopping recording RecordingId={RecordingId}", request.RecordingId);

        await _callAutomation.GetCallRecording().StopAsync(request.RecordingId, ct);

        // Update MEETING_RECORDING row
        var entity = await _recordings.GetByRecordingIdAsync(request.RecordingId, ct);
        if (entity == null)
        {
            _logger.LogWarning(
                "No MeetingRecordingEntity found for RecordingId={RecordingId} when stopping.",
                request.RecordingId);
            return;
        }

        var endUtc = DateTime.UtcNow;
        entity.RecordingEndUtc = endUtc;

        if (entity.RecordingStartUtc != default)
        {
            entity.DurationSeconds = (int)(endUtc - entity.RecordingStartUtc).TotalSeconds;
        }

        await _recordings.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Recording stopped. RecordingId={RecordingId}, DurationSeconds={DurationSeconds}",
            request.RecordingId,
            entity.DurationSeconds);
    }


    public async Task<IReadOnlyList<MeetingRecordingDto>> ListAsync(
    string? meetingId,
    CancellationToken ct = default)
    {
        _logger.LogInformation("Listing recordings for MeetingId={MeetingId}", meetingId);

        var entities = await _recordings.ListByMeetingIdAsync(meetingId, ct);

        var container = _blobServiceClient.GetBlobContainerClient(_containerName);

        // Generate one SAS for the container
        var containerSasUrl = SasHelpers.GenerateSasUrl(container);

        var dtos = entities
            .OrderByDescending(x => x.RecordingStartUtc)
            .Select(x => new MeetingRecordingDto
            {
                RecordingId = x.RecordingId,
                MeetingId = x.MeetingId,
                GroupId = x.GroupId,
                BlobName = x.BlobName,
                BlobUrl = $"{containerSasUrl}/{x.BlobName}", // reuse container SAS
                RecordingStartUtc = x.RecordingStartUtc,
                DurationSeconds = x.DurationSeconds,
                AdviserName = x.Meeting.Adviser.FullName,
                ClientName = x.Meeting.Lead.ClientName,
                MeetingDate = x.Meeting.StartUtc,
                ClientEntityID = x.Meeting.LeadId,
                MeetingType = x.Meeting.MeetingType,
                MeetingTitle = x.Meeting.Title,
                AdviserEmail = x.Meeting.Adviser.Email
            })
            .ToList();


        return dtos;
    }

    public async Task<MeetingRecordingDto?> GetAsync(
        string recordingId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recordingId))
            throw new ArgumentException("recordingId is required.", nameof(recordingId));

        _logger.LogInformation("Fetching recording metadata for RecordingId={RecordingId}", recordingId);

        var entity = await _recordings.GetRecordingWithMeetingAndClientAsync(recordingId, ct);
        if (entity == null)
        {
            _logger.LogWarning("No recording found in DB for RecordingId={RecordingId}", recordingId);
            return null;
        }

        string? sasUrl = null;

        if (!string.IsNullOrWhiteSpace(entity.BlobName))
        {
            // 🔹 Normalise BlobName so it does NOT include the container prefix
            var blobName = entity.BlobName;

            if (!string.IsNullOrEmpty(_containerName) &&
                blobName.StartsWith(_containerName + "/", StringComparison.OrdinalIgnoreCase))
            {
                blobName = blobName.Substring(_containerName.Length + 1);
                _logger.LogInformation(
                    "Normalised BlobName from '{Original}' to '{Normalised}' for RecordingId={RecordingId}",
                    entity.BlobName,
                    blobName,
                    recordingId);
            }

            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = container.GetBlobClient(blobName);

            sasUrl = SasHelpers.GenerateSasUrl(blobClient);
        }
        else
        {
            _logger.LogWarning(
                "RecordingId={RecordingId} has no BlobName set. Using stored BlobUrl as-is.",
                recordingId);

            sasUrl = entity.BlobUrl;
        }

        return new MeetingRecordingDto
        {
            RecordingId = entity.RecordingId,
            MeetingId = entity.MeetingId,
            GroupId = entity.GroupId,
            BlobName = entity.BlobName,
            BlobUrl = sasUrl,                 
            RecordingStartUtc = entity.RecordingStartUtc,
            //RecordingEndUtc = entity.RecordingEndUtc,
            DurationSeconds = entity.DurationSeconds,

            AdviserName = entity.Meeting.Adviser.FullName,
            AdviserEmail = entity.Meeting.Adviser.Email,
            ClientName = entity.Meeting.Lead.ClientName,
            ClientEntityID = entity.Meeting.LeadId,
            MeetingDate = entity.Meeting.StartUtc,
            MeetingType = entity.Meeting.MeetingType,
            MeetingTitle = entity.Meeting.Title
        };
    }
}
