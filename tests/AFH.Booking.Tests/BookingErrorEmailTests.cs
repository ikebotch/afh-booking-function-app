using System.Text.Json;
using AFH.Booking.Function.Middleware;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.Email.Builders;
using AFH.Common.Errors.Email.Options;

namespace AFH.Booking.Tests;

public sealed class BookingErrorEmailTests
{
    [Fact]
    public void BookingHandledErrorEmailPolicy_DoesNotNotifyForValidationErrors()
    {
        var mapping = new BookingExceptionMapper().TryMap(new JsonException("Bad JSON"));

        Assert.NotNull(mapping);
        Assert.False(BookingHandledErrorEmailPolicy.ShouldNotify(mapping!.MappingResult));
    }

    [Fact]
    public void BookingHandledErrorEmailPolicy_BuildsSharedEmailForServerErrors()
    {
        var mapping = new BookingExceptionMapper().TryMap(new InvalidOperationException("Setting is required."));

        Assert.NotNull(mapping);
        Assert.True(BookingHandledErrorEmailPolicy.ShouldNotify(mapping!.MappingResult));

        var record = new ErrorRecordBuilder().Build(mapping.MappingResult);
        var request = BookingHandledErrorEmailPolicy.CreateNotificationRequest("BookingFunction", mapping.MappingResult.StatusCode, record);
        var builder = new ErrorEmailMessageBuilder();
        var model = builder.BuildTemplateModel(request, new ErrorEmailOptions
        {
            ToAddresses = ["ops@example.com"],
            SubjectPrefix = "[AFH Booking Error]"
        });
        var body = builder.BuildBody(model);

        Assert.Equal("[AFH Booking Error] Error: ConfigurationError", model.Subject);
        Assert.Equal("booking", model.Metadata["service"]);
        Assert.Equal("500", model.Metadata["statusCode"]);
        Assert.Contains("Booking handled exception in BookingFunction.", body);
    }
}
