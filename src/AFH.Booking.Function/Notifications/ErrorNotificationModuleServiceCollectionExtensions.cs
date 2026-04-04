using AFH.Common.Errors.Email.DependencyInjection;
using AFH.Common.Errors.Email.Models;
using AFH.Common.Errors.Email.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Function.Notifications;

internal static class ErrorNotificationModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingErrorNotificationModule(
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
        var settings = configuration.GetSection("ErrorEmail").Get<ErrorEmailConfiguration>() ?? new ErrorEmailConfiguration();

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

    internal sealed class ErrorEmailConfiguration
    {
        public string? FromAddress { get; init; }
        public string? FromDisplayName { get; init; }
        public string? ToAddresses { get; init; }
        public string? CcAddresses { get; init; }
        public string? BccAddresses { get; init; }
        public string? SubjectPrefix { get; init; }
        public bool? IncludeDetails { get; init; }
    }
}
