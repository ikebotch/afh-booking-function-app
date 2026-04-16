using AFH.Acs.Function.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Function.Services.Recordings;

public static class RecordingServiceCollectionExtensions
{
    public static IServiceCollection AddRecordingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RecordingOptions>()
            .Bind(configuration.GetSection(RecordingOptions.SectionName));
        services.AddSingleton<MetadataMeetingRecordingService>();
        services.AddSingleton<LiveAcsMeetingRecordingService>();
        services.AddSingleton<IMeetingRecordingService>(sp =>
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
