using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Policies.Booking;
using AFH.Notification.Application.Services;
using AFH.Notification.Contract.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Notification.Application.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationPublisher, NotificationOutboxService>();

        services.AddScoped<INotificationIdempotencyKeyGenerator, NotificationIdempotencyKeyGenerator>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<INotificationIdempotencyPolicy, BookingNotificationIdempotencyPolicy>();
        services.AddScoped<INotificationRoutingPolicy, BookingNotificationRoutingPolicy>();
        services.AddScoped<INotificationTemplatePolicy, BookingNotificationTemplatePolicy>();

        return services;
    }
}
