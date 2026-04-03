using AFH.Booking.Application.Composition;
using AFH.Booking.Function.Middleware;
using AFH.Booking.Infrastructure.Composition;
using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using Azure.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddAfhCommonErrorsAzureFunctions();
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
