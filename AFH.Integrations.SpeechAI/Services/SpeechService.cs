using AFH.Integrations.SpeechAI.Configuration;
using AFH.Integrations.SpeechAI.Models.Requests;
using AFH.Integrations.SpeechAI.Models.Responses;
using AFH.Integrations.SpeechAI.Services.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription;

using static AFH.Integrations.SpeechAI.Constants.ConnectorConstants;
using System.Net;

namespace AFH.Integrations.SpeechAI.Services
{
    public class SpeechService : ISpeechService

    {
        private Connector _connector;
        private readonly JsonSerializerSettings _serializerSettings;
		private readonly IOptions<SpeechAIConfig> _options;

		public SpeechService(IOptions<SpeechAIConfig> options)
        {
          //  _connector = new Connector();
            _options = options;
			_connector = new Connector(_options);
            _serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        } 
        
        public async Task<JobStatusResponse> StartJob(string fileUrl)
		{
			var requestBody = new TranscriptionJobStartRequest(fileUrl);
            var requestJson = JsonConvert.SerializeObject(requestBody, _serializerSettings);
            using (var content = new StringContent(requestJson, System.Text.Encoding.Default, HeaderConstants.MediaTypeJson))
            {
                var response =  await _connector.PostAsync<JobStatusResponse>(Endpoints.Base, content);

                return response;    
            }
		}


        public async Task<JobStatusResponse> CheckJobStatus(string jobId)
        {
            var response = await _connector.GetAsync<JobStatusResponse>(String.Format(Endpoints.StatusCheck, jobId));

            return response;
        }


        public async Task<JobFilesResponse> GetJobFiles(string jobId)
        {
            var response = await _connector.GetAsync<JobFilesResponse>(String.Format(Endpoints.GetTranscriptionFiles, jobId));

            return response;
        }

        public async Task<TranscriptFileResponse> GetTranscript(string fileUrl)
        {
            var _httpClient = new HttpClient
            {
                BaseAddress = new Uri(fileUrl)
            };

            var response = await _httpClient.GetAsync("");
            var result = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TranscriptFileResponse>(result, _serializerSettings);

        }
    }
}
