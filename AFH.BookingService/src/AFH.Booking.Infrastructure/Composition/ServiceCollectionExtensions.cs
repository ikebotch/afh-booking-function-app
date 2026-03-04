using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Calendar;
using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Infrastructure.Location;
using AFH.Booking.Infrastructure.Meetings;
using AFH.Booking.Infrastructure.Options;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

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
        services.Configure<TravelMatrixOptions>(config.GetSection(TravelMatrixOptions.SectionName));
        services.Configure<AzureAdOptions>(config.GetSection(AzureAdOptions.SectionName));
        services.Configure<LocationServiceOptions>(config.GetSection(LocationServiceOptions.SectionName));
        services.Configure<CalendarSubscriptionOptions>(config.GetSection(CalendarSubscriptionOptions.SectionName));
        services.Configure<GraphWebhookOptions>(config.GetSection(GraphWebhookOptions.SectionName));

        var db = config.GetSection(BookingDbOptions.SectionName).Get<BookingDbOptions>()
                 ?? throw new InvalidOperationException($"{BookingDbOptions.SectionName} config is missing.");

        if (string.IsNullOrWhiteSpace(db.ConnectionString))
            throw new InvalidOperationException($"{BookingDbOptions.SectionName}:ConnectionString is required.");

        services.AddDbContext<BookingDbContext>(opt => { opt.UseSqlServer(db.ConnectionString); });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Calendar service integration (AFH.Calendar function app)
        services.AddHttpClient<ICalendarGateway, CalendarGateway>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<CalendarSubscriptionOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException($"{CalendarSubscriptionOptions.SectionName}:BaseUrl is required.");

            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<ITravelMatrixService, TravelMatrixService>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<LocationServiceOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var tenantId = cfg["CalendarGraph:TenantId"];
            var clientId = cfg["CalendarGraph:ClientId"];
            var secret = cfg["CalendarGraph:ClientSecret"];

            var cred = new ClientSecretCredential(tenantId, clientId, secret);
            return new GraphServiceClient(cred, new[] { "https://graph.microsoft.com/.default" });
        });

        services.AddScoped<ICalendarSubscriptionGateway, CalendarSubscriptionGateway>();

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
        services.AddScoped<ICalendarEventSnapshotRepository, CalendarEventSnapshotRepository>();
        services.AddScoped<ICalendarSubscriptionRepository, CalendarSubscriptionRepository>();
        services.AddScoped<ICalendarNotificationRepository, CalendarNotificationRepository>();

        return services;
    }
}
