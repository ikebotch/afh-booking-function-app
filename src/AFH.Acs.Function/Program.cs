using AFH.Acs.Function.Configuration;
using AFH.Acs.Function.Notifications;
using AFH.Acs.Function.Middleware;
using AFH.Acs.Function.Options;
using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using AFH.Acs.Infrastructure.Extensions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(app =>
    {
        ConfigureMiddlewarePipeline(app);
    })
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        ConfigureAppConfiguration(cfg);
    })
    .ConfigureLogging((ctx, logging) =>
    {
        ConfigureLogging(logging);
    })
    .ConfigureServices((ctx, services) =>
    {
        AddSharedErrorHandling(services, ctx.Configuration, "[AFH ACS Error]", "acs");
        AddValidatedSecurityOptions(services, ctx.Configuration);
        services.AddAfhAcsInfrastructure(ctx.Configuration);
        ConfigureWorkerSerialization(services, caseInsensitivePropertyNames: true);
    })
    .Build();

host.Run();


static void ConfigureMiddlewarePipeline(IFunctionsWorkerApplicationBuilder app)
{
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<OperationAuditMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<InternalApiAuthMiddleware>();
}

static void ConfigureLogging(ILoggingBuilder logging)
{
    logging.ClearProviders();
    logging.AddConsole();

    logging.AddFilter("Microsoft", LogLevel.Warning);
    logging.AddFilter("System", LogLevel.Warning);
    logging.AddFilter("AFH.Acs.Functions", LogLevel.Information);
    logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
    logging.AddFilter("AFH.Acs.Recorder", LogLevel.Information);
    logging.AddFilter("AFH.Acs.Functions.Functions", LogLevel.Information);
    logging.AddFilter("Default", LogLevel.Warning);
    logging.AddFilter("Microsoft.Azure.WebJobs", LogLevel.Information);
    logging.AddFilter("Microsoft.Azure.WebJobs.Hosting", LogLevel.Information);
}
static void ConfigureAppConfiguration(IConfigurationBuilder cfg)
{
    cfg.AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables();
}

static void AddSharedErrorHandling(
    IServiceCollection services,
    IConfiguration configuration,
    string defaultSubjectPrefix,
    string serviceName)
{
    services.AddApplicationInsightsTelemetryWorkerService();
    services.AddAfhCommonErrorsAzureFunctions();
    var enableErrorEmail = configuration.GetValue("ErrorEmail:Enabled", false);
    if (enableErrorEmail)
    {
        services.AddAcsErrorNotificationModule(configuration, defaultSubjectPrefix, serviceName);
    }
    services.AddSingleton<AcsExceptionMapper>();
    services.AddSingleton<IExceptionMapper>(sp => sp.GetRequiredService<AcsExceptionMapper>());
}

static void AddValidatedSecurityOptions(IServiceCollection services, IConfiguration configuration)
{
    services.AddSingleton<IValidateOptions<InternalApiAuthOptions>, InternalApiAuthOptionsValidator>();
    services.AddOptions<InternalApiAuthOptions>()
        .Bind(configuration.GetSection(InternalApiAuthOptions.SectionName))
        .ValidateOnStart();
}

static void ConfigureWorkerSerialization(IServiceCollection services, bool caseInsensitivePropertyNames)
{
    services.Configure<WorkerOptions>(options =>
    {
        options.Serializer = new JsonObjectSerializer(
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = caseInsensitivePropertyNames
            });
    });
}
