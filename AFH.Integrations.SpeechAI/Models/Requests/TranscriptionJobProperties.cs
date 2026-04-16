using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.SpeechAI.Models.Requests
{
	public  class TranscriptionJobProperties
	{
		[JsonProperty("diarizationEnabled")]
		public bool DiarizationEnabled { get; set; }

		[JsonProperty("diarization")]
		public Speaker Diarization { get; set; }
	}

	public class Speaker
	{
		[JsonProperty("speakers")]
		public SpeakerProperties Properties { get; set; }
	}

	public class SpeakerProperties
	{
		[JsonProperty("minCount")]
		public int MinCount { get; set; }

		[JsonProperty("maxCount")]
		public int MaxCount { get; set; }
	}
}
