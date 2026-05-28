using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Services;
using AFH.Notification.Infrastructure.Delivery.Email;
using AFH.Notification.Infrastructure.Delivery.Email.Graph;
using AFH.Notification.Infrastructure.Delivery.Sms;
using AFH.Notification.Infrastructure.Integration;
using AFH.Notification.Infrastructure.Options;
using AFH.Notification.Infrastructure.Bouncebacks;
using AFH.Notification.Infrastructure.Persistence;
using AFH.Notification.Infrastructure.Queue;
using AFH.Notification.Contract.Abstractions;
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
        services.Configure<AzureCommunicationSmsOptions>(configuration.GetSection(AzureCommunicationSmsOptions.SectionName));
        services.Configure<TwilioSmsOptions>(configuration.GetSection(TwilioSmsOptions.SectionName));
        services.Configure<PushDeliveryOptions>(configuration.GetSection(PushDeliveryOptions.SectionName));
        services.Configure<NotificationQueueOptions>(options => BindNotificationQueueOptions(configuration, options));
        services.Configure<NotificationIntegrationOptions>(configuration.GetSection(NotificationIntegrationOptions.SectionName));
        services.Configure<HttpNotificationPublisherOptions>(configuration.GetSection(HttpNotificationPublisherOptions.SectionName));
        services.Configure<InternalApiAuthTokenOptions>(configuration.GetSection(InternalApiAuthTokenOptions.SectionName));
        services.Configure<ServiceBusNotificationPublisherOptions>(configuration.GetSection(ServiceBusNotificationPublisherOptions.SectionName));

        var connectionString = configuration.GetConnectionString("BookingDb")
            ?? configuration["Values:ConnectionStrings:BookingDb"]
            ?? configuration["ConnectionStrings:BookingDb"]
            ?? configuration["Values:BookingDb:ConnectionString"]
            ?? throw new InvalidOperationException("BookingDb connection string is not configured.");

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<INotificationOutboxStore, NotificationOutboxStore>();
        AddQueuePublisher(services, configuration);

        services.AddScoped<INotificationAuditStore, NotificationAuditStore>();
        services.AddScoped<INotificationDeliveryAuditStore, NotificationDeliveryAuditStore>();
        services.AddScoped<INotificationTemplateStore, NotificationTemplateStore>();
        services.AddScoped<INotificationTemplateAdminStore, NotificationTemplateAdminStore>();
        services.AddScoped<INotificationStatusService, NotificationStatusService>();
        AddEmailDeliveryGateway(services, configuration);
        AddSmsDeliveryGateway(services, configuration);
        services.AddScoped<IContactCentreRoutingResolver, ContactCentreRoutingResolver>();

        services.AddSingleton<EmailBouncebackParser>();
        services.AddScoped<INotificationBouncebackStore, EmailBouncebackStore>();
        services.AddScoped<INotificationBounceAuditStore, EmailBouncebackStore>();
        services.AddScoped<INotificationBouncebackProcessor, EmailBouncebackProcessor>();
        AddSourcePublisher(services, configuration);

        return services;
    }

    private static void AddSourcePublisher(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(NotificationIntegrationOptions.SectionName).Get<NotificationIntegrationOptions>()
            ?? new NotificationIntegrationOptions();

        switch (options.Transport?.Trim().ToUpperInvariant())
        {
            case "SERVICEBUS":
                services.AddScoped<INotificationPublisher, ServiceBusNotificationPublisher>();
                return;
            case "INPROCESS":
                services.AddScoped<INotificationPublisher, InProcessNotificationPublisher>();
                return;
            case null:
            case "":
            case "HTTP":
                services.AddHttpClient<INotificationPublisher, HttpNotificationPublisher>((sp, http) =>
                {
                    var httpOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HttpNotificationPublisherOptions>>().Value;
                    if (!string.IsNullOrWhiteSpace(httpOptions.BaseUrl))
                        http.BaseAddress = new Uri(httpOptions.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

                    http.Timeout = TimeSpan.FromSeconds(httpOptions.TimeoutSeconds <= 0 ? 30 : httpOptions.TimeoutSeconds);
                });
                return;
            default:
                throw new InvalidOperationException($"{NotificationIntegrationOptions.SectionName}:Transport must be Http, ServiceBus, or InProcess.");
        }
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

    private static void AddSmsDeliveryGateway(IServiceCollection services, IConfiguration configuration)
    {
        var smsOptions = configuration.GetSection(SmsDeliveryOptions.SectionName).Get<SmsDeliveryOptions>()
            ?? new SmsDeliveryOptions();

        if (!smsOptions.Enabled ||
            string.IsNullOrWhiteSpace(smsOptions.ProviderName) ||
            string.Equals(smsOptions.ProviderName, "Composed", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<INotificationDeliveryGateway, SmsNotificationDeliveryGateway>();
            return;
        }

        if (string.Equals(smsOptions.ProviderName, "AzureCommunicationServices", StringComparison.OrdinalIgnoreCase))
        {
            var acsOptions = configuration.GetSection(AzureCommunicationSmsOptions.SectionName).Get<AzureCommunicationSmsOptions>()
                ?? new AzureCommunicationSmsOptions();
            acsOptions.Validate();
            services.AddHttpClient<AzureCommunicationSmsSender>();
            services.AddScoped<ISmsProviderSender>(sp => sp.GetRequiredService<AzureCommunicationSmsSender>());
            services.AddScoped<INotificationDeliveryGateway, SmsNotificationDeliveryGateway>();
            return;
        }

        if (string.Equals(smsOptions.ProviderName, "Twilio", StringComparison.OrdinalIgnoreCase))
        {
            var twilioOptions = configuration.GetSection(TwilioSmsOptions.SectionName).Get<TwilioSmsOptions>()
                ?? new TwilioSmsOptions();
            twilioOptions.Validate();
            services.AddHttpClient<TwilioSmsSender>(http =>
            {
                http.BaseAddress = new Uri("https://api.twilio.com/", UriKind.Absolute);
            });
            services.AddScoped<ISmsProviderSender>(sp => sp.GetRequiredService<TwilioSmsSender>());
            services.AddScoped<INotificationDeliveryGateway, SmsNotificationDeliveryGateway>();
            return;
        }

        throw new InvalidOperationException($"{SmsDeliveryOptions.SectionName}:ProviderName must be Composed, AzureCommunicationServices, or Twilio.");
    }

    private static void AddQueuePublisher(IServiceCollection services, IConfiguration configuration)
    {
        var queueOptions = new NotificationQueueOptions();
        BindNotificationQueueOptions(configuration, queueOptions);
        queueOptions.ValidateForAzureQueueMode();
        services.AddScoped<INotificationQueuePublisher, AzureStorageNotificationQueuePublisher>();
    }

    private static void BindNotificationQueueOptions(IConfiguration configuration, NotificationQueueOptions options)
    {
        configuration.GetSection(NotificationQueueOptions.SectionName).Bind(options);
    }
}
