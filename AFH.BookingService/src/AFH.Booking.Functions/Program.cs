using AFH.Booking.Application.Composition;
using AFH.Booking.Infrastructure.Composition;
using Azure.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        // Load configuration from local.settings.json and environment variables
        cfg.SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables();

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
        logging.AddFilter("AFH.Acs.Recorder", LogLevel.Information);

        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
        logging.AddFilter("AFH.Acs.Recorder.Functions", LogLevel.Information);
        logging.AddFilter("Default", LogLevel.Warning);

        logging.AddFilter("Microsoft.Azure.WebJobs", LogLevel.Information);
        logging.AddFilter("Microsoft.Azure.WebJobs.Hosting", LogLevel.Information);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddBookingApplication();
        services.AddBookingInfrastructure(ctx.Configuration);
        services.AddHttpClient();

        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
                });
        });
    })
    .Build();

host.Run();
