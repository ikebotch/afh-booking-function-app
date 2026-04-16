using Newtonsoft.Json;
using AFH.Integrations.SpeechAI.Models.Base;

namespace AFH.Integrations.SpeechAI.Models.Requests
{
	public class TranscriptionJobStartRequest : BaseSpeechModel
	{

		public TranscriptionJobStartRequest(string fileUrl)
		{
			DisplayName = "Transcription of my audio file";
			Description = "Batch transcription demo";
			Locale = "en-US";
			Urls = new List<string>() { fileUrl };
			Properties = new TranscriptionJobProperties
			{
				DiarizationEnabled = true,
				Diarization = new Speaker { Properties = new SpeakerProperties { MinCount = 1, MaxCount = 10 } }
			};
		}

		[JsonProperty("contentUrls")]

		public List<string> Urls { get; set; }

		[JsonProperty("properties")]
		public TranscriptionJobProperties Properties { get; set; }

	}


}
