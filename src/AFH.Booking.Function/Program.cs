using AFH.Booking.Application.Composition;
using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Configuration;
using AFH.Booking.Function.Middleware;
using AFH.Booking.Infrastructure.Composition;
using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.ApplicationInsights.DependencyInjection;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using AFH.Common.Errors.Email.DependencyInjection;
using AFH.Common.Errors.Email.Models;
using AFH.Common.Errors.Email.Options;
using Azure.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
        AddSharedErrorHandling(services, ctx.Configuration, "[AFH Booking Error]", "booking");

        services.AddBookingApplication();
        services.AddBookingInfrastructure(ctx.Configuration);
        services.AddHttpClient();

        AddValidatedSecurityOptions(services, ctx.Configuration);
        ConfigureWorkerSerialization(services, caseInsensitivePropertyNames: true);
    })
    .Build();

host.Run();

static void ConfigureMiddlewarePipeline(dynamic app)
{
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<OperationAuditMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<InternalApiAuthMiddleware>();
    app.UseMiddleware<DomainUserAuthMiddleware>();
}

static void ConfigureAppConfiguration(IConfigurationBuilder cfg)
{
    // Load configuration from local.settings.json and environment variables.
    // Support running from either function project folder or solution root.
    var cwd = Directory.GetCurrentDirectory();
    var localSettingsInCwd = Path.Combine(cwd, "local.settings.json");
    var localSettingsFromSolutionRoot = Path.Combine(cwd, "src", "AFH.Booking.Function", "local.settings.json");

    cfg.SetBasePath(cwd);

    if (File.Exists(localSettingsInCwd))
        cfg.AddJsonFile(localSettingsInCwd, optional: true, reloadOnChange: true);

    if (File.Exists(localSettingsFromSolutionRoot))
        cfg.AddJsonFile(localSettingsFromSolutionRoot, optional: true, reloadOnChange: true);

    cfg.AddEnvironmentVariables();
    AddFlattenedValuesSection(cfg);
}

static void AddFlattenedValuesSection(IConfigurationBuilder cfg)
{
    var values = cfg.Build()
        .GetSection("Values")
        .AsEnumerable()
        .Where(kv => kv.Value is not null)
        .ToDictionary(kv => kv.Key.Replace("Values:", ""), kv => kv.Value!);

    cfg.AddInMemoryCollection(values);
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

static void AddSharedErrorHandling(
    IServiceCollection services,
    IConfiguration configuration,
    string defaultSubjectPrefix,
    string serviceName)
{
    services.AddApplicationInsightsTelemetryWorkerService();
    services.AddAfhCommonErrorsApplicationInsights();
    services.AddAfhCommonErrorsAzureFunctions();
    services.AddAfhCommonErrorsEmail(
        BuildErrorEmailOptions(configuration, defaultSubjectPrefix),
        sp => CreateErrorEmailSender(sp, serviceName));
    services.AddSingleton<BookingExceptionMapper>();
    services.AddSingleton<IExceptionMapper>(sp => sp.GetRequiredService<BookingExceptionMapper>());
}

static void AddValidatedSecurityOptions(IServiceCollection services, IConfiguration configuration)
{
    services.AddSingleton<IValidateOptions<InternalApiAuthOptions>, InternalApiAuthOptionsValidator>();
    services.AddSingleton<IValidateOptions<DomainUserAuthOptions>, DomainUserAuthOptionsValidator>();

    services.AddOptions<InternalApiAuthOptions>()
        .Bind(configuration.GetSection(InternalApiAuthOptions.SectionName))
        .ValidateOnStart();

    services.AddOptions<DomainUserAuthOptions>()
        .Bind(configuration.GetSection(DomainUserAuthOptions.SectionName))
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

static ErrorEmailOptions BuildErrorEmailOptions(IConfiguration configuration, string defaultSubjectPrefix)
{
    var settings = configuration.GetSection("ErrorEmail").Get<ErrorEmailConfiguration>() ?? new ErrorEmailConfiguration();

    return new ErrorEmailOptions
    {
        FromAddress = settings.FromAddress,
        FromDisplayName = settings.FromDisplayName,
        ToAddresses = SplitAddresses(settings.ToAddresses),
        CcAddresses = SplitAddresses(settings.CcAddresses),
        BccAddresses = SplitAddresses(settings.BccAddresses),
        SubjectPrefix = string.IsNullOrWhiteSpace(settings.SubjectPrefix) ? defaultSubjectPrefix : settings.SubjectPrefix!,
        IncludeDetails = settings.IncludeDetails ?? true
    };
}

static IReadOnlyCollection<string> SplitAddresses(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return [];

    return value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static Func<ErrorEmailTemplateModel, string, CancellationToken, Task> CreateErrorEmailSender(IServiceProvider serviceProvider, string serviceName)
{
    var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AFH.Common.Errors.Email");

    return (model, _, _) =>
    {
        if (model.ToAddresses.Count > 0)
        {
            logger.LogDebug(
                "Prepared handled error email notification for Service={Service} Subject={Subject} RecipientCount={RecipientCount}, but no service-local transport is configured.",
                serviceName,
                model.Subject,
                model.ToAddresses.Count);
        }

        return Task.CompletedTask;
    };
}

internal sealed class ErrorEmailConfiguration
{
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }
    public string? ToAddresses { get; init; }
    public string? CcAddresses { get; init; }
    public string? BccAddresses { get; init; }
    public string? SubjectPrefix { get; init; }
    public bool? IncludeDetails { get; init; }
}
