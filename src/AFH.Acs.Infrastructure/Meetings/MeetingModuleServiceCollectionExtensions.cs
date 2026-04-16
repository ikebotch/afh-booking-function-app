using AFH.Acs.Application.Abstractions;
using AFH.Acs.Application.Services;
using AFH.Acs.Infrastructure.Acs;
using AFH.Acs.Infrastructure.Options;
using AFH.Acs.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Infrastructure.Extensions;

internal static class MeetingModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddMeetingModule(this IServiceCollection services)
    {
        services.AddScoped<IMeetingSessionRepository, MeetingSessionRepository>();
        services.AddScoped<IJoinTokenIssuer, AcsJoinTokenIssuer>();
        services.AddScoped<IMeetingSessionService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AcsFrontendOptions>>().Value;
            return new MeetingSessionService(
                sp.GetRequiredService<IMeetingSessionRepository>(),
                sp.GetRequiredService<IJoinTokenIssuer>(),
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
