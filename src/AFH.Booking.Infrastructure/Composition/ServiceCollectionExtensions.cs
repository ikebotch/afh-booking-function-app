using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Abstractions.Notifications;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Approvals;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Bookings;
using AFH.Booking.Infrastructure.Calendar;
using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Infrastructure.Location;
using AFH.Booking.Infrastructure.Logging;
using AFH.Booking.Infrastructure.Meetings;
using AFH.Booking.Infrastructure.Notifications;
using AFH.Booking.Infrastructure.Notifications.Options;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using AFH.Common.Errors.ApplicationInsights.DependencyInjection;
using AFH.Common.Errors.EntityFramework.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookingInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<BookingDbOptions>(config.GetSection(BookingDbOptions.SectionName));
        services.Configure<CalendarOptions>(config.GetSection(CalendarOptions.SectionName));
        services.Configure<LeadsOptions>(config.GetSection(LeadsOptions.SectionName));
        services.Configure<AcsOptions>(config.GetSection(AcsOptions.SectionName));
        services.Configure<XPlanOptions>(config.GetSection(XPlanOptions.SectionName));
        services.Configure<AzureAdOptions>(config.GetSection(AzureAdOptions.SectionName));
        services.Configure<LocationServiceOptions>(config.GetSection(LocationServiceOptions.SectionName));
        services.Configure<CalendarSubscriptionOptions>(config.GetSection(CalendarSubscriptionOptions.SectionName));
        services.Configure<NotificationsOptions>(config.GetSection(NotificationsOptions.SectionName));
        services.Configure<BookingChangeAccessOptions>(config.GetSection(BookingChangeAccessOptions.SectionName));
        services.Configure<ApprovalRoutingOptions>(config.GetSection(ApprovalRoutingOptions.SectionName));
        services.Configure<LifecycleReasonOptions>(config.GetSection(LifecycleReasonOptions.SectionName));
        services.Configure<LifecycleNotificationOptions>(config.GetSection(LifecycleNotificationOptions.SectionName));
        services.Configure<LifecycleEscalationOptions>(config.GetSection(LifecycleEscalationOptions.SectionName));
        services.Configure<LifecycleGovernanceOptions>(config.GetSection(LifecycleGovernanceOptions.SectionName));
        services.Configure<OutlookGovernanceOptions>(config.GetSection(OutlookGovernanceOptions.SectionName));
        services.Configure<AdviserDirectoryOptions>(config.GetSection(AdviserDirectoryOptions.SectionName));
        services.Configure<NotificationApiPublisherOptions>(config.GetSection(NotificationApiPublisherOptions.SectionName));
        services.Configure<AvailabilityRulesOptions>(config.GetSection(AvailabilityRulesOptions.SectionName));
        services.Configure<ApplicationLoggingOptions>(config.GetSection(ApplicationLoggingOptions.SectionName));
        services.Configure<FinalRouteTimeGuardOptions>(config.GetSection(FinalRouteTimeGuardOptions.SectionName));
        services.AddSingleton<IInternalServiceAuthenticator, InternalBearerServiceAuthenticator>();
        services.AddSingleton<IEntraTokenValidator, EntraTokenValidator>();
        services.AddSingleton<ICurrentUserProfileResolver, DomainUserProfileResolver>();

        var bookingDbConnectionString = ResolveBookingDbConnectionString(config);

        if (string.IsNullOrWhiteSpace(bookingDbConnectionString))
            throw new InvalidOperationException(
                $"{BookingDbOptions.SectionName}:ConnectionString is required (or ConnectionStrings:BookingDb).");

        services.AddDbContext<BookingDbContext>(opt => { opt.UseSqlServer(bookingDbConnectionString); });
        services.AddDbContextFactory<BookingDbContext>(
            options =>
            {
                options.UseSqlServer(bookingDbConnectionString);
            },
            ServiceLifetime.Scoped);
        services.AddAfhCommonErrorsApplicationInsights();
        services.AddAfhCommonErrorsEntityFramework<BookingDbContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseApplicationLogSink>();
        services.AddScoped<BookingHandledErrorTelemetryEmitter>();
        services.AddScoped<ApplicationInsightsLogSink>(sp => new ApplicationInsightsLogSink(
            sp.GetService<Microsoft.ApplicationInsights.TelemetryClient>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ApplicationInsightsLogSink>>()));
        services.AddScoped<IApplicationLogSink>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ApplicationLoggingOptions>>().Value;
            return options.Provider switch
            {
                ApplicationLogProvider.Database => sp.GetRequiredService<DatabaseApplicationLogSink>(),
                ApplicationLogProvider.ApplicationInsights => sp.GetRequiredService<ApplicationInsightsLogSink>(),
                _ => new CompositeApplicationLogSink(
                    sp.GetRequiredService<DatabaseApplicationLogSink>(),
                    sp.GetRequiredService<ApplicationInsightsLogSink>())
            };
        });

        // Calendar service integration (AFH.Calendar function app)
        services.AddHttpClient<ICalendarGateway, CalendarGateway>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<CalendarSubscriptionOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException($"{CalendarSubscriptionOptions.SectionName}:BaseUrl is required.");

            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<ILocationTravelCoverageClient, LocationTravelCoverageClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<LocationServiceOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<ILocationRouteTimeClient, LocationRouteTimeClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<LocationServiceOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        // Leads integration
        services.AddHttpClient<LeadsAccessToken>();
        services.AddHttpClient<IClientDirectory, LeadsClientDirectory>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<LeadsOptions>>().Value;

            if (string.IsNullOrWhiteSpace(o.BaseUrl))
                throw new InvalidOperationException($"{LeadsOptions.SectionName}:BaseUrl is required.");

            http.BaseAddress = new Uri(o.BaseUrl, UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds <= 0 ? 30 : o.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(o.BearerToken))
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", o.BearerToken);
            }
        });

        services.AddScoped<IClientEnricher, XPlanClientEnricher>();

        // ACS meeting details integration
        services.AddHttpClient<IMeetingLinkFactory, AcsMeetingLinkFactory>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<AcsOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(o.MeetingLinkServiceBaseUrl))
                http.BaseAddress = new Uri(o.MeetingLinkServiceBaseUrl, UriKind.Absolute);

            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IBookingTransactionRepository, BookingTransactionRepository>();
        services.AddScoped<IBookingSlotRepository, BookingSlotRepository>();
        services.AddScoped<IBookingHoldRepository, BookingHoldRepository>();
        services.AddScoped<IAdviserProfileProjectionRepository, AdviserProfileProjectionRepository>();
        services.AddScoped<IIntegrationSyncStateRepository, IntegrationSyncStateRepository>();
        services.AddScoped<ILifecycleEventRepository, LifecycleEventRepository>();
        services.AddScoped<IMeetingTopicRepository, MeetingTopicRepository>();
        services.AddScoped<IMeetingTypeRepository, MeetingTypeRepository>();
        services.AddScoped<ILifecycleStepRepository, LifecycleStepRepository>();
        services.AddScoped<IBookingNotificationPolicyProvider, BookingNotificationPolicyProvider>();
        services.AddScoped<IBookingNotificationRecipientResolver, BookingNotificationRecipientResolver>();
        services.AddHttpClient<IBookingBusinessContactsClient, BookingBusinessContactsClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<LocationServiceOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

            http.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<IBookingNotificationPublisher, NotificationApiPublisher>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<NotificationApiPublisherOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException($"{NotificationApiPublisherOptions.SectionName}:BaseUrl is required for booking notification HTTP publishing.");

            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });
        services.AddScoped<IOperationalIssueRepository, OperationalIssueRepository>();
        services.AddScoped<IBookingChangeAccessService, HmacBookingChangeAccessService>();
        services.AddScoped<IApprovalRoutingService, ConfigurationApprovalRoutingService>();
        services.AddScoped<IApprovalNotificationService, ApprovalNotificationService>();
        services.AddScoped<IApprovalWorkflowStore, DbApprovalWorkflowStore>();
        services.AddHttpClient<IAdviserProjectionSyncService, AdviserProjectionSyncService>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<AdviserDirectoryOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

            http.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHostedService<AdviserProjectionSyncWorker>();
        services.AddScoped<IDuplicateClientService, DuplicateClientService>();
        services.AddScoped<IDownstreamUpdateService, DownstreamUpdateService>();
        services.AddScoped<IDownstreamUpdateReconciliationService, DownstreamUpdateService>();

        services.AddHttpClient<IAdminCoverageService, AdminCoverageService>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<LocationServiceOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHostedService<BookingOperationalStoreInitializer>();
        services.AddSingleton(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
        });

        return services;
    }

    internal static string? ResolveBookingDbConnectionString(IConfiguration config)
    {
        var options = config.GetSection(BookingDbOptions.SectionName).Get<BookingDbOptions>() ?? new BookingDbOptions();
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            return options.ConnectionString;

        return config.GetConnectionString("BookingDb")
            ?? config["Values:ConnectionStrings:BookingDb"]
            ?? config["Values:BookingDb:ConnectionString"];
    }
}
