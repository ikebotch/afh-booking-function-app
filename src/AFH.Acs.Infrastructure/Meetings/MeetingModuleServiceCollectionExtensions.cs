using AFH.Acs.Application.Abstractions.Advisers;
using AFH.Acs.Application.Abstractions.Identity;
using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Application.Services.Meetings;
using AFH.Acs.Infrastructure.Identity;
using AFH.Acs.Infrastructure.Options;
using AFH.Acs.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Infrastructure.Meetings;

internal static class MeetingModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddMeetingModule(this IServiceCollection services)
    {
        services.AddScoped<IMeetingSessionRepository, MeetingSessionRepository>();
        services.AddScoped<IMeetingTranscriptionRepository, MeetingTranscriptionRepository>();
        services.AddScoped<IJoinTokenIssuer, AcsJoinTokenIssuer>();
        services.AddScoped<IMeetingSessionService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AcsFrontendOptions>>().Value;
            return new MeetingSessionService(
                sp.GetRequiredService<IMeetingSessionRepository>(),
                sp.GetRequiredService<IJoinTokenIssuer>(),
                sp.GetRequiredService<IAdviserInfoProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MeetingSessionService>>(),
                options.JoinBaseUrl);
        });
        services.AddScoped<IMeetingLinkService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AcsFrontendOptions>>().Value;
            return new MeetingLinkService(options.JoinBaseUrl);
        });

        return services;
    }
}
