using System.Text.Json;
using AFH.Booking.Function.Middleware;
using AFH.Common.Errors.ApplicationInsights.Telemetry;
using AFH.Common.Errors.Builders;

namespace AFH.Booking.Tests;

public sealed class BookingErrorTelemetryTests
{
    [Fact]
    public void ErrorTelemetryBuilder_BuildsHandledBookingTelemetry()
    {
        var mapping = new BookingExceptionMapper().TryMap(new JsonException("Bad JSON"));

        Assert.NotNull(mapping);

        var record = new ErrorRecordBuilder().Build(mapping!.MappingResult);
        var builder = new ErrorTelemetryBuilder(new ErrorTelemetryMapper(), new ErrorTelemetryEnricher());
        var telemetry = builder.Build(record, (properties, _) =>
        {
            properties["afh.service"] = "booking";
            properties["afh.function.name"] = "booking-test";
        });

        Assert.Equal("afh.common_errors", telemetry.Name);
        Assert.Equal("InvalidJson", telemetry.Properties["afh.error.code"]);
        Assert.Equal("Validation", telemetry.Properties["afh.error.category"]);
        Assert.Equal("booking", telemetry.Properties["afh.service"]);
        Assert.Equal("booking-test", telemetry.Properties["afh.function.name"]);
    }
}
