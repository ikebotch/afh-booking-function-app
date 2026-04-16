using AFH.Acs.Infrastructure.Logging;
using AFH.Acs.Infrastructure.Advisers;
using AFH.Acs.Infrastructure.Identity;
using AFH.Acs.Infrastructure.Meetings;
using AFH.Acs.Infrastructure.Recordings;
using AFH.Acs.Infrastructure.Transcription;
using AFH.Acs.Infrastructure.Options;
using AFH.Acs.Infrastructure.Persistence;
using AFH.Acs.Infrastructure.Persistence.Repositories;
using AFH.Common.Errors.ApplicationInsights.DependencyInjection;
using AFH.Common.Errors.EntityFramework.DependencyInjection;
using AFH.Common.SpeechAI.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Acs.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAfhAcsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AcsFrontendOptions>(configuration.GetSection(AcsFrontendOptions.SectionName));
        services.Configure<ApplicationLoggingOptions>(configuration.GetSection(ApplicationLoggingOptions.SectionName));

        var acsConnectionString = configuration["Acs:ConnectionString"]
            ?? throw new InvalidOperationException("Missing Acs:ConnectionString.");

        var dbConnectionString = configuration["MeetingDb:ConnectionString"] ?? configuration["MSSQL_CONN"];
        if (string.IsNullOrWhiteSpace(dbConnectionString))
            throw new InvalidOperationException("Missing MeetingDb:ConnectionString or MSSQL_CONN.");

        services.AddDbContext<MeetingDbContext>(options => options.UseSqlServer(dbConnectionString));
        services.AddDbContextFactory<MeetingDbContext>(
         options => options.UseSqlServer(dbConnectionString),
         ServiceLifetime.Scoped);
        services.AddAfhCommonErrorsApplicationInsights();
        services.AddAfhCommonErrorsEntityFramework<MeetingDbContext>();
        services.AddScoped<DatabaseApplicationLogSink>();
        services.AddScoped<AcsHandledErrorTelemetryEmitter>();
        services.AddScoped<ApplicationInsightsLogSink>(sp => new ApplicationInsightsLogSink(
            sp.GetService<Microsoft.ApplicationInsights.TelemetryClient>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ApplicationInsightsLogSink>>()));
        services.AddScoped<IApplicationLogSink>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApplicationLoggingOptions>>().Value;
            return options.Provider switch
            {
                ApplicationLogProvider.Database => sp.GetRequiredService<DatabaseApplicationLogSink>(),
                ApplicationLogProvider.ApplicationInsights => sp.GetRequiredService<ApplicationInsightsLogSink>(),
                _ => new CompositeApplicationLogSink(
                    sp.GetRequiredService<DatabaseApplicationLogSink>(),
                    sp.GetRequiredService<ApplicationInsightsLogSink>())
            };
        });
        services.AddAdviserInfoModule(configuration);
        services.AddMeetingModule();
        services.AddIdentityModule(acsConnectionString);
        services.AddRecordingModule(configuration);
        services.AddSpeechAi(configuration);
        services.AddTranscriptionModule();

        return services;
    }
}
