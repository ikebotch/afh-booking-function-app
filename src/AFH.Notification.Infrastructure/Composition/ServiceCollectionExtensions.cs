using AFH.Notification.Application.Abstractions;
using AFH.Notification.Infrastructure.Delivery.Email;
using AFH.Notification.Infrastructure.Options;
using AFH.Notification.Infrastructure.Bouncebacks;
using AFH.Notification.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<EmailDeliveryOptions>(configuration.GetSection(EmailDeliveryOptions.SectionName));
        services.Configure<SmsDeliveryOptions>(configuration.GetSection(SmsDeliveryOptions.SectionName));
        services.Configure<PushDeliveryOptions>(configuration.GetSection(PushDeliveryOptions.SectionName));

        var connectionString = configuration.GetConnectionString("BookingDb")
            ?? configuration["Values:ConnectionStrings:BookingDb"]
            ?? configuration["Values:BookingDb:ConnectionString"];

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<NotificationDbContext>(options =>
                options.UseSqlServer(connectionString));
        }

        services.AddScoped<INotificationOutboxStore, NotificationOutboxStore>();

        services.AddScoped<INotificationAuditStore, NotificationAuditStore>();
        services.AddScoped<INotificationDeliveryGateway, EmailNotificationDeliveryGateway>();
        services.AddScoped<IContactCentreRoutingResolver, ContactCentreRoutingResolver>();

        services.AddSingleton<EmailBouncebackParser>();
        services.AddScoped<INotificationBouncebackStore, EmailBouncebackStore>();
        services.AddScoped<INotificationBouncebackProcessor, EmailBouncebackProcessor>();

        return services;
    }
}
