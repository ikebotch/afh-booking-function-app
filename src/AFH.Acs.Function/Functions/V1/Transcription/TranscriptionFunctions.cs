using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.Transcription;

public sealed class TranscriptionFunctions(ITranscriptionWorkflowService transcriptionService)
{
    [Function("v1-transcriptions-status")]
    public async Task<HttpResponseData> GetStatusAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/transcriptions/{jobId}")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var result = await transcriptionService.GetStatusAsync(jobId, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }

    [Function("v1-transcriptions-files")]
    public async Task<HttpResponseData> GetFilesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/transcriptions/{jobId}/files")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var result = await transcriptionService.GetFilesAsync(jobId, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }

    [Function("v1-transcriptions-content")]
    public async Task<HttpResponseData> GetContentAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/transcriptions/{jobId}/content")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var result = await transcriptionService.GetContentAsync(jobId, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }

    [Function("v1-transcriptions-speaker-content")]
    public async Task<HttpResponseData> GetSpeakerContentAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/transcriptions/{jobId}/speaker-content")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        var result = await transcriptionService.GetSpeakerFormattedTranscriptAsync(jobId, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(result, ct);
        return response;
    }

    [Function("v1-transcriptions-cancel")]
    public async Task<HttpResponseData> CancelAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/transcriptions/{jobId}/cancel")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        await transcriptionService.CancelAsync(jobId, ct);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("v1-transcriptions-delete")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/transcriptions/{jobId}")] HttpRequestData req,
        string jobId,
        CancellationToken ct)
    {
        await transcriptionService.DeleteAsync(jobId, ct);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
