using AFH.Booking.Application;
using AFH.Booking.Application.Calendar.Options;
using AFH.Booking.Infrastructure;
using AFH.Common.CalendarUtils.Sdk.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var built = cfg.Build();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in built.GetSection("Values").GetChildren())
            values[child.Key] = child.Value;

        if (values.Count > 0)
            cfg.AddInMemoryCollection(values);
    })
    .ConfigureServices((ctx, services) =>
    {
        // Logging + AppInsights
        services.AddApplicationInsightsTelemetryWorkerService();
        //services.ConfigureFunctionsApplicationInsights();

       
        services.AddBookingApplication(ctx.Configuration);
        services.AddBookingInfrastructure(ctx.Configuration);

        services.Configure<GraphWebhookOptions>(ctx.Configuration.GetSection("GraphWebhook"));

    })
    .Build();

host.Run();
