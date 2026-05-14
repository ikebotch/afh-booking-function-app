using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Logging;

internal sealed class ApplicationInsightsLogSink : IApplicationLogSink
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApplicationInsightsLogSink> _logger;

    public ApplicationInsightsLogSink(
        TelemetryClient? telemetryClient,
        IConfiguration configuration,
        ILogger<ApplicationInsightsLogSink> logger)
    {
        _telemetryClient = telemetryClient;
        _configuration = configuration;
        _logger = logger;
    }

    public Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_telemetryClient is null || string.IsNullOrWhiteSpace(_configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
                return Task.CompletedTask;

            var properties = new Dictionary<string, string>
            {
                ["category"] = entry.Category,
                ["operation"] = entry.Operation,
                ["eventType"] = entry.EventType,
                ["result"] = entry.Result
            };

            AddProperty(properties, "correlationId", entry.CorrelationId);
            AddProperty(properties, "userId", entry.UserId);
            AddProperty(properties, "contextId", entry.ContextId);
            AddProperty(properties, "exceptionType", entry.ExceptionType);
            AddProperty(properties, "payloadJson", entry.PayloadJson);

            if (!string.IsNullOrWhiteSpace(entry.ExceptionMessage))
            {
                var exception = new InvalidOperationException(entry.ExceptionMessage);
                _telemetryClient.TrackException(exception, properties);
            }
            else
            {
                _telemetryClient.TrackTrace(entry.Message, ToSeverityLevel(entry.Level), properties);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write booking application log to Application Insights.");
        }

        return Task.CompletedTask;
    }

    private static void AddProperty(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            properties[key] = value;
    }

    private static SeverityLevel ToSeverityLevel(string level) =>
        level.ToLowerInvariant() switch
        {
            "critical" => SeverityLevel.Critical,
            "error" => SeverityLevel.Error,
            "warning" => SeverityLevel.Warning,
            "debug" => SeverityLevel.Verbose,
            _ => SeverityLevel.Information
        };
}
