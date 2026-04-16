using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Helpers;
using AFH.Acs.Recorder.Infrastructure.Data.SharePointListFields;
using AFH.Acs.Recorder.Models.V1;
using AFH.Acs.Recorder.Services.Interface;
using AFH.Integrations.Sharepoint.Services;
using AFH.Integrations.SpeechAI.Extension;
using AFH.Integrations.SpeechAI.Services.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
namespace AFH.Acs.Recorder.Services.Lookup;

public sealed class TranscriptionService : ITranscriptionService
{
    private readonly SharepointService _sharepointService;
    private readonly SharePointConfigWrapper _spConfig;
    private readonly ISpeechService _speechService;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<TranscriptionService> _logger;
    private readonly string _containerName;
    private readonly IConfiguration _config;


    public TranscriptionService(
        SharepointService sharepointService,
        IOptions<SharePointConfigWrapper> options,
        ISpeechService speechService,
        BlobServiceClient blobServiceClient,
        ILogger<TranscriptionService> logger,
        IConfiguration config)
    {
        _sharepointService = sharepointService ?? throw new ArgumentNullException(nameof(sharepointService));
        _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config;
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _spConfig = options.Value ?? throw new InvalidOperationException("SharePointConfigs is not bound.");

        if (_spConfig.ClientTranscriptionListConfigs is null)
        {
            throw new InvalidOperationException(
                "SharePointConfigs:ClientTranscriptionListConfigs is not configured. " +
                "Ensure you have a client transcription list configured under 'SharePointConfigs:ClientTranscriptionListConfigs'.");
        }
        var conn = _config["AzureWebJobsAudioFileStorage"]
                     ?? throw new InvalidOperationException("AzureWebJobsAudioFileStorage not configured.");
        _containerName = config["Storage:Blob:Container"] ?? "recordings";
        _blobServiceClient = new BlobServiceClient(conn);
    }


    public async Task<IReadOnlyList<RecordingTranscriptionRequest>> GetTranscriptionDataAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var clientConfig = _spConfig.ClientTranscriptionListConfigs;
        var expandFields = new[] { "fields" };

        IReadOnlyList<ListItem> listItems;

        try
        {
            listItems = await _sharepointService.GetListItems(
                clientConfig.SiteId,
                clientConfig.ListId,
                expandFields: expandFields
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve transcription list items from SharePoint. SiteId={SiteId}, ListId={ListId}",
                clientConfig.SiteId,
                clientConfig.ListId);

            return Array.Empty<RecordingTranscriptionRequest>();
        }

        var dtos = listItems
            .Select(MapFromSharePointItem)
            .ToList();

        return dtos;
    }

    private static RecordingTranscriptionRequest MapFromSharePointItem(ListItem item)
    {
        var data = item.Fields.AdditionalData;

        return new RecordingTranscriptionRequest
        {
            RecordingId = data.GetString(TranscriptionFields.RecordingId),
            ClientName = data.GetString(TranscriptionFields.ClientName),
            MeetingDate = data.GetDateTime(TranscriptionFields.MeetingDate),
            ClientEntityID = data.GetString(TranscriptionFields.ClientEntityID),
            Filename = data.GetString(TranscriptionFields.Filename),
            MeetingType = data.GetString(TranscriptionFields.MeetingType),
            NotesStatus = data.GetString(TranscriptionFields.NotesStatus),
            AttitudetoRisk = data.GetString(TranscriptionFields.AttitudetoRisk),
            FinancialGoals = data.GetString(TranscriptionFields.FinancialGoals),
            Tax = data.GetString(TranscriptionFields.Tax),
            Transcription = data.GetString(TranscriptionFields.Transcription),
            // ToDo: include RecordingId/MeetingId 
        };
    }


    public async Task<TranscriptionRunResult> TranscribeRecordingAsync(
        RecordingTranscriptionRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        ct.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Starting transcription for RecordingId={RecordingId}, BlobName={BlobName}, MeetingId={MeetingId}",
            request.RecordingId,
            request.BlobName,
            request.MeetingId);

        // Get the blob

        var container = _blobServiceClient.GetBlobContainerClient(_containerName);


        var blobName = request.BlobName;
        if (blobName.StartsWith(_containerName + "/", StringComparison.OrdinalIgnoreCase))
        {
            blobName = blobName.Substring(_containerName.Length + 1);
        }

        var blobClient = container.GetBlobClient(blobName);


        if (!await blobClient.ExistsAsync(ct))
        {
            var msg = $"Blob does not exist for recording. BlobName={request.BlobName}";
            _logger.LogWarning(msg);
            return new TranscriptionRunResult
            {
                RecordingId = request.RecordingId,
                Succeeded = false,
                FailureReason = msg
            };
        }
        var sasUrl = SasHelpers.GenerateSasUrl(blobClient);


        _logger.LogInformation(
            "Generated SAS URL for transcription. RecordingId={RecordingId}, Url={Url}",
            request.RecordingId,
            sasUrl);

        // Kick off Speech job
        var job = await _speechService.StartJob(sasUrl);
        _logger.LogInformation("Speech job started. JobId={JobId}, Status={Status}",
            job.JobId, job.Status);

        // Poll status
        do
        {
            job = await _speechService.CheckJobStatus(job.JobId);
            _logger.LogInformation("Speech job status. JobId={JobId}, Status={Status}",
                job.JobId, job.Status);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        } while (job.Status == "Running" || job.Status == "NotStarted");




        if (!string.Equals(job.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var msg = $"Speech job did not succeed. JobId={job.JobId}, Status={job.Status}, FailureReason={job.FailureReason}";
            _logger.LogWarning(msg);

            return new TranscriptionRunResult
            {
                RecordingId = request.RecordingId,
                Succeeded = false,
                FailureReason = msg
            };
        }

        // Fetch transcription files

        var files = await _speechService.GetJobFiles(job.JobId);

        //if (files?.Items.Any() == true)
        //{
        //    var msg = $"No transcription files returned by Speech for JobId={job.JobId}.";
        //    _logger.LogWarning(msg);
        //    return new TranscriptionRunResult
        //    {
        //        RecordingId = request.RecordingId,
        //        Succeeded = false,
        //        FailureReason = msg
        //    };
        //}

        var transcriptItem = files.Items.FirstOrDefault(x => x.Kind == "Transcription");
        if (transcriptItem == null)
        {
            var msg = $"No item of kind 'Transcription' found for JobId={job.JobId}.";
            _logger.LogWarning(msg);
            return new TranscriptionRunResult
            {
                RecordingId = request.RecordingId,
                Succeeded = false,
                FailureReason = msg
            };
        }

        var transcriptRaw = await _speechService.GetTranscript(transcriptItem.File.Url);

        var maskedITN = transcriptRaw.GetMaskedITNWithDiarization();



        _logger.LogInformation(
            "Transcription completed. RecordingId={RecordingId}, TranscriptLength={Length}",
            request.RecordingId,
            maskedITN?.Length ?? 0);

        // Write to SharePoint transcription list
        var clientConfig = _spConfig.ClientTranscriptionListConfigs;

        var spData = new Dictionary<string, object>
        {
            { TranscriptionFields.Transcription,  maskedITN ?? string.Empty },
            { TranscriptionFields.ClientName,     request.ClientName ?? string.Empty },
            { TranscriptionFields.MeetingDate,    request.MeetingDate.ToString("o") ?? string.Empty },
            { TranscriptionFields.ClientEntityID, request.ClientEntityID.ToString() ?? string.Empty },
            { TranscriptionFields.Filename,       request.BlobName },
            { TranscriptionFields.MeetingType,    request.MeetingType ?? string.Empty },
            { TranscriptionFields.NotesStatus,    "0" }, // e.g. 0 = New
            { TranscriptionFields.Adviser,        request.AdviserEmail ?? string.Empty },
             { TranscriptionFields.RecordingId,   request.RecordingId },
            // ToDo:  add RecordingId/MeetingId as columns in the SP list
            // { TranscriptionFields.MeetingId,     request.MeetingId }
        };

        var savedSP = await _sharepointService.AddListItem(
            clientConfig.SiteId,
            clientConfig.ListId,
            spData);

        _logger.LogInformation(
"Transcription SharePoint item created. RecordingId={RecordingId}, SiteId={SiteId}, ListId={ListId}",
request.RecordingId,
clientConfig.SiteId, savedSP.Id, savedSP.SharepointIds,
clientConfig.ListId);

        return new TranscriptionRunResult
        {
            RecordingId = request.RecordingId,
            Transcript = maskedITN,
            Succeeded = true
        };
    }
}