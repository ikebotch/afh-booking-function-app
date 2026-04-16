using System.Text.Json;
using AFH.Acs.Function.Middleware;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.Email.Builders;
using AFH.Common.Errors.Email.Options;

namespace AFH.Acs.Tests;

public sealed class AcsErrorEmailTests
{
    [Fact]
    public void AcsHandledErrorEmailPolicy_DoesNotNotifyForValidationErrors()
    {
        var mapping = new AcsExceptionMapper().Map(new JsonException("Bad JSON"));

        Assert.False(AcsHandledErrorEmailPolicy.ShouldNotify(mapping));
    }

    [Fact]
    public void AcsHandledErrorEmailPolicy_BuildsSharedEmailForServerErrors()
    {
        var mapping = new AcsExceptionMapper().Map(new Exception("Boom"));

        Assert.True(AcsHandledErrorEmailPolicy.ShouldNotify(mapping));

        var record = new ErrorRecordBuilder().Build(mapping);
        var request = AcsHandledErrorEmailPolicy.CreateNotificationRequest("AcsFunction", mapping.StatusCode, record);
        var builder = new ErrorEmailMessageBuilder();
        var model = builder.BuildTemplateModel(request, new ErrorEmailOptions
        {
            ToAddresses = ["ops@example.com"],
            SubjectPrefix = "[AFH ACS Error]"
        });
        var body = builder.BuildBody(model);

        Assert.Equal("[AFH ACS Error] Error: INTERNAL_ERROR", model.Subject);
        Assert.Equal("acs", model.Metadata["service"]);
        Assert.Equal("500", model.Metadata["statusCode"]);
        Assert.Contains("ACS handled exception in AcsFunction.", body);
    }
}
