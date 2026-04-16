using AFH.Integrations.SpeechAI.Services;
using AFH.Integrations.SpeechAI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Integrations.SpeechAI.Configuration
{
	public static class SpeechAiIntegration
	{
		public static IServiceCollection Register(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddOptions<SpeechAIConfig>()
				.Configure<IConfiguration>((settings, configuration) =>
				{
					configuration.GetSection("SpeechAI").Bind(settings);
				});
			
			services.AddSingleton<ISpeechService, SpeechService>();

			return services;

		}
	}
}
