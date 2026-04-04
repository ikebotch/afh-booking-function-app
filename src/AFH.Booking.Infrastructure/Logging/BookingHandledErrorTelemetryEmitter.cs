using AFH.Common.Errors.ApplicationInsights.Telemetry;
using AFH.Common.Errors.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace AFH.Booking.Infrastructure.Logging;

public sealed class BookingHandledErrorTelemetryEmitter
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly ErrorTelemetryBuilder? _telemetryBuilder;

    public BookingHandledErrorTelemetryEmitter(
        TelemetryClient? telemetryClient,
        ErrorTelemetryBuilder? telemetryBuilder)
    {
        _telemetryClient = telemetryClient;
        _telemetryBuilder = telemetryBuilder;
    }

    public void Track(ErrorRecord record, string functionName)
    {
        if (_telemetryClient is null || _telemetryBuilder is null)
            return;

        var telemetry = _telemetryBuilder.Build(record, (properties, _) =>
        {
            properties["afh.service"] = "booking";
            properties["afh.function.name"] = functionName;
        });

        var eventTelemetry = new EventTelemetry(telemetry.Name)
        {
            Timestamp = telemetry.Timestamp
        };

        foreach (var pair in telemetry.Properties)
        {
            if (pair.Value is not null)
                eventTelemetry.Properties[pair.Key] = pair.Value;
        }

        foreach (var metric in telemetry.Metrics)
            eventTelemetry.Properties[metric.Key] = metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        _telemetryClient.TrackEvent(eventTelemetry);
    }
}
