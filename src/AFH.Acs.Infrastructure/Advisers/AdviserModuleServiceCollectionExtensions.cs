using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AFH.Acs.Application.Abstractions.Advisers;

namespace AFH.Acs.Infrastructure.Advisers;

public static class AdviserModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAdviserInfoModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMemoryCache();
        services.AddOptions<LocationAdviserInfoOptions>()
            .Bind(configuration.GetSection(LocationAdviserInfoOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Location:BaseUrl must be an absolute URI.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CoveragePath), "Location:CoveragePath is required.")
            .ValidateOnStart();

        services.AddHttpClient<LocationAdviserInfoProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LocationAdviserInfoOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IAdviserInfoProvider>(sp =>
            sp.GetRequiredService<LocationAdviserInfoProvider>());

        return services;
    }
}
