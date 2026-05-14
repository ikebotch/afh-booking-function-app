using AFH.Common.Errors.Email.DependencyInjection;
using AFH.Common.Errors.Email.Models;
using AFH.Common.Errors.Email.Options;
using AFH.Booking.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Composition;

public static class ErrorNotificationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddBookingErrorNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultSubjectPrefix,
        string serviceName)
    {
        services.AddAfhCommonErrorsEmail(
            BuildErrorEmailOptions(configuration, defaultSubjectPrefix),
            sp => CreateErrorEmailSender(sp, serviceName));

        return services;
    }

    private static ErrorEmailOptions BuildErrorEmailOptions(IConfiguration configuration, string defaultSubjectPrefix)
    {
        var settings = configuration.GetSection(ErrorEmailOptionsConfiguration.SectionName).Get<ErrorEmailOptionsConfiguration>()
            ?? new ErrorEmailOptionsConfiguration();

        return new ErrorEmailOptions
        {
            FromAddress = settings.FromAddress,
            FromDisplayName = settings.FromDisplayName,
            ToAddresses = SplitAddresses(settings.ToAddresses),
            CcAddresses = SplitAddresses(settings.CcAddresses),
            BccAddresses = SplitAddresses(settings.BccAddresses),
            SubjectPrefix = string.IsNullOrWhiteSpace(settings.SubjectPrefix) ? defaultSubjectPrefix : settings.SubjectPrefix!,
            IncludeDetails = settings.IncludeDetails ?? true
        };
    }

    private static IReadOnlyCollection<string> SplitAddresses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Func<ErrorEmailTemplateModel, string, CancellationToken, Task> CreateErrorEmailSender(
        IServiceProvider serviceProvider,
        string serviceName)
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

}
