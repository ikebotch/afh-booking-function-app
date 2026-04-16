using AFH.Integrations.SpeechAI.Models.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Integrations.SpeechAI.Extension
{
	public static class TranscriptFileResponseExtension
	{
		public static string GetMaskedITNWithDiarization(this TranscriptFileResponse model)
		{
			var responseText = String.Empty;
			var currentSpeaker = 0;
			if(model != null && model.RecognisedPhrases != null)
			{
				var t = model.RecognisedPhrases.Select(x => new { x.Speaker,  x.TranscriptFilePhrases });
				foreach (var phrase in model.RecognisedPhrases)
				{
					var str = String.Empty;
					if(phrase.Speaker != currentSpeaker)
					{
						str = $"<br> Speaker: {phrase.Speaker}";
						currentSpeaker = phrase.Speaker;
					}
					responseText += $"{str} {phrase.TranscriptFilePhrases?.FirstOrDefault()?.MaskedITN}";
				}


			}


			return responseText;
		}
	}
}
