using AFH.Notification.Application.Abstractions;
using AFH.Notification.Infrastructure.Delivery.Email;
using AFH.Notification.Infrastructure.Delivery.Email.Graph;
using AFH.Notification.Infrastructure.Options;
using AFH.Notification.Infrastructure.Bouncebacks;
using AFH.Notification.Infrastructure.Persistence;
using AFH.Notification.Infrastructure.Queue;
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
        services.Configure<GraphEmailOptions>(configuration.GetSection(GraphEmailOptions.SectionName));
        services.Configure<SmsDeliveryOptions>(configuration.GetSection(SmsDeliveryOptions.SectionName));
        services.Configure<PushDeliveryOptions>(configuration.GetSection(PushDeliveryOptions.SectionName));
        services.Configure<NotificationQueueOptions>(configuration.GetSection(NotificationQueueOptions.SectionName));

        var connectionString = configuration.GetConnectionString("BookingDb")
            ?? configuration["Values:ConnectionStrings:BookingDb"]
            ?? configuration["Values:BookingDb:ConnectionString"];

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<NotificationDbContext>(options =>
                options.UseSqlServer(connectionString));
        }

        services.AddScoped<INotificationOutboxStore, NotificationOutboxStore>();
        services.AddScoped<INotificationQueuePublisher, AzureStorageNotificationQueuePublisher>();

        services.AddScoped<INotificationAuditStore, NotificationAuditStore>();
        AddEmailDeliveryGateway(services, configuration);
        services.AddScoped<IContactCentreRoutingResolver, ContactCentreRoutingResolver>();

        services.AddSingleton<EmailBouncebackParser>();
        services.AddScoped<INotificationBouncebackStore, EmailBouncebackStore>();
        services.AddScoped<INotificationBouncebackProcessor, EmailBouncebackProcessor>();

        return services;
    }

    private static void AddEmailDeliveryGateway(IServiceCollection services, IConfiguration configuration)
    {
        var emailOptions = configuration.GetSection(EmailDeliveryOptions.SectionName).Get<EmailDeliveryOptions>()
            ?? new EmailDeliveryOptions();

        if (!emailOptions.Enabled ||
            string.Equals(emailOptions.ProviderName, "Composed", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(emailOptions.ProviderName))
        {
            services.AddScoped<INotificationDeliveryGateway, EmailNotificationDeliveryGateway>();
            return;
        }

        if (string.Equals(emailOptions.ProviderName, "Graph", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GraphEmailOptions>>().Value;
                return GraphEmailClientFactory.Create(options);
            });
            services.AddScoped<IGraphEmailSender, GraphEmailSender>();
            services.AddScoped<INotificationDeliveryGateway, GraphEmailDeliveryGateway>();
            return;
        }

        services.AddScoped<INotificationDeliveryGateway, EmailNotificationDeliveryGateway>();
    }
}
