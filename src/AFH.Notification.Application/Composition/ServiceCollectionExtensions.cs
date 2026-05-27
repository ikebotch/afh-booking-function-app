using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Options;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Application.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationPublisher, NotificationOutboxService>();
        services.AddScoped<NotificationOutboxDispatcher>();

        services.AddScoped<INotificationIdempotencyKeyGenerator, NotificationIdempotencyKeyGenerator>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<INotificationIdempotencyPolicy, BookingNotificationIdempotencyPolicy>();
        services.AddScoped<INotificationRoutingPolicy, BookingNotificationRoutingPolicy>();
        services.AddScoped<INotificationTemplatePolicy, BookingNotificationTemplatePolicy>();
        services.AddSingleton<IValidateOptions<NotificationOutboxDispatchOptions>, NotificationOutboxDispatchOptionsValidator>();

        return services;
    }
}

public sealed class NotificationOutboxDispatchOptionsValidator : IValidateOptions<NotificationOutboxDispatchOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationOutboxDispatchOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
