using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Application.Services.Transcription;
using AFH.Common.SpeechAI.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Acs.Infrastructure.Transcription;

public static class TranscriptionModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTranscriptionModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ISpeechTranscriptionClient, SpeechAiTranscriptionClient>();
        services.AddScoped<ITranscriptionWorkflowService, TranscriptionWorkflowService>();
        return services;
    }
}
