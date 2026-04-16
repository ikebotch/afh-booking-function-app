using AFH.Integrations.SpeechAI.Models.Responses;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.SpeechAI.Services.Interfaces
{
    public interface ISpeechService
    {
        Task<JobStatusResponse> StartJob(string fileUrl);
        Task<JobStatusResponse> CheckJobStatus(string jobId);
        Task<JobFilesResponse> GetJobFiles(string jobId);
        Task<TranscriptFileResponse> GetTranscript(string fileUrl);
    }
}
