using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Infrastructure.Options;
using AFH.Acs.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Infrastructure.Recordings;

public static class RecordingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddRecordingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RecordingOptions>()
            .Bind(configuration.GetSection(RecordingOptions.SectionName))
            .Validate(options => Enum.IsDefined(typeof(RecordingMode), options.Mode), "Recording:Mode is invalid.")
            .ValidateOnStart();

        services.AddScoped<IMeetingRecordingRepository, MeetingRecordingRepository>();
        services.AddScoped<MetadataMeetingRecordingService>();
        services.AddScoped<LiveAcsMeetingRecordingService>();
        services.AddScoped<IMeetingRecordingService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RecordingOptions>>().Value;
            return options.Mode switch
            {
                RecordingMode.LiveAcs => sp.GetRequiredService<LiveAcsMeetingRecordingService>(),
                _ => sp.GetRequiredService<MetadataMeetingRecordingService>()
            };
        });

        return services;
    }
}
