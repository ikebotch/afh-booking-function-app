using System.Text.Json;
using AFH.Acs.Function.Middleware;
using AFH.Common.Errors.ApplicationInsights.Telemetry;
using AFH.Common.Errors.Builders;

namespace AFH.Acs.Tests;

public sealed class AcsErrorTelemetryTests
{
    [Fact]
    public void ErrorTelemetryBuilder_BuildsHandledAcsTelemetry()
    {
        var mapping = new AcsExceptionMapper().Map(new JsonException("Bad JSON"));
        var record = new ErrorRecordBuilder().Build(mapping);
        var builder = new ErrorTelemetryBuilder(new ErrorTelemetryMapper(), new ErrorTelemetryEnricher());
        var telemetry = builder.Build(record, (properties, _) =>
        {
            properties["afh.service"] = "acs";
            properties["afh.function.name"] = "acs-test";
        });

        Assert.Equal("afh.common_errors", telemetry.Name);
        Assert.Equal("VALIDATION_ERROR", telemetry.Properties["afh.error.code"]);
        Assert.Equal("Validation", telemetry.Properties["afh.error.category"]);
        Assert.Equal("acs", telemetry.Properties["afh.service"]);
        Assert.Equal("acs-test", telemetry.Properties["afh.function.name"]);
    }
}
