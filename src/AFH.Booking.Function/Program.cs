using AFH.Booking.Application.Composition;
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

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(app =>
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<OperationAuditMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<InternalApiAuthMiddleware>();
        app.UseMiddleware<DomainUserAuthMiddleware>();
    })
    .ConfigureAppConfiguration((ctx, cfg) =>
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

        // Flatten "Values" section into top-level keys
        var values = cfg.Build()
            .GetSection("Values")
            .AsEnumerable()
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key.Replace("Values:", ""), kv => kv.Value!);

        cfg.AddInMemoryCollection(values);
    })
    .ConfigureLogging((ctx, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();

        // Fine-grained filters
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("AFH.Acs.Functions", LogLevel.Information);

        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
        logging.AddFilter("AFH.Acs.Recorder", LogLevel.Information);
        logging.AddFilter("AFH.Acs.Functions.Functions", LogLevel.Information);
        logging.AddFilter("Default", LogLevel.Warning);

        logging.AddFilter("Microsoft.Azure.WebJobs", LogLevel.Information);
        logging.AddFilter("Microsoft.Azure.WebJobs.Hosting", LogLevel.Information);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddAfhCommonErrorsApplicationInsights();
        services.AddAfhCommonErrorsAzureFunctions();
        services.AddAfhCommonErrorsEmail(
            BuildErrorEmailOptions(ctx.Configuration, "[AFH Booking Error]"),
            sp => CreateErrorEmailSender(sp, "booking"));
        services.AddSingleton<BookingExceptionMapper>();
        services.AddSingleton<IExceptionMapper>(sp => sp.GetRequiredService<BookingExceptionMapper>());
        services.AddBookingApplication();
        services.AddBookingInfrastructure(ctx.Configuration);
        services.AddHttpClient();
        services.Configure<InternalApiAuthOptions>(ctx.Configuration.GetSection(InternalApiAuthOptions.SectionName));
        services.Configure<DomainUserAuthOptions>(ctx.Configuration.GetSection(DomainUserAuthOptions.SectionName));

        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
        });

    })
    .Build();

host.Run();

static ErrorEmailOptions BuildErrorEmailOptions(IConfiguration configuration, string defaultSubjectPrefix)
{
    var section = configuration.GetSection("ErrorEmail");

    return new ErrorEmailOptions
    {
        FromAddress = section["FromAddress"],
        FromDisplayName = section["FromDisplayName"],
        ToAddresses = SplitAddresses(section["ToAddresses"]),
        CcAddresses = SplitAddresses(section["CcAddresses"]),
        BccAddresses = SplitAddresses(section["BccAddresses"]),
        SubjectPrefix = string.IsNullOrWhiteSpace(section["SubjectPrefix"]) ? defaultSubjectPrefix : section["SubjectPrefix"]!,
        IncludeDetails = !bool.TryParse(section["IncludeDetails"], out var includeDetails) || includeDetails
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
