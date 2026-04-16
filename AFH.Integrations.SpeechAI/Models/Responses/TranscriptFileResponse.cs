using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.SpeechAI.Models.Responses
{
    public class TranscriptFileResponse
    {
        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("durationInTicks")]
        public long DurationTicks { get; set; }

        [JsonProperty("durationMilliseconds")]
        public long DurationMilliseconds { get; set; }

        [JsonProperty("combinedRecognizedPhrases")]
        public IEnumerable<TranscriptFilePhrase> TranscriptFilePhrases { get; set; }

		[JsonProperty("recognizedPhrases")]
		public IEnumerable<RecognisedPhrase> RecognisedPhrases { get; set; }
	}

    public class TranscriptFilePhrase
    {
        [JsonProperty("channel")]
        public int Channel { get; set; }

        [JsonProperty("lexical")]
        public string Lexical { get; set; }

        [JsonProperty("itn")]
        public string Itn { get; set; }

        [JsonProperty("maskedITN")]
        public string MaskedITN { get; set; }

        [JsonProperty("display")]
        public string Display { get; set; }
    }

    public class RecognisedPhrase
    {
		[JsonProperty("channel")]
		public int Channel { get; set; }

		[JsonProperty("recognitionStatus")]
		public string Status { get; set; }

		[JsonProperty("speaker")]
		public int Speaker { get; set; }

		[JsonProperty("nBest")]
		public IEnumerable<TranscriptFilePhrase> TranscriptFilePhrases { get; set; }
	}
}
