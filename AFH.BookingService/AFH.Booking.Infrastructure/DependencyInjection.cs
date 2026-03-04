using AFH.Booking.Infrastructure.Calendar;
using AFH.Booking.Infrastructure.Calendar.Stores;
using AFH.Booking.Infrastructure.Clock;
using AFH.Booking.Infrastructure.Options;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using AFH.Common.CalendarUtils.Sdk.Extensions;

namespace AFH.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // DB options
        services.AddOptions<BookingDbOptions>()
            .Bind(config.GetSection(BookingDbOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "BookingDb:ConnectionString is required.")
            .ValidateOnStart();

        // EF Core
        services.AddDbContext<BookingDbContext>((sp, opts) =>
        {
            var db = sp.GetRequiredService<IOptions<BookingDbOptions>>().Value;
            opts.UseSqlServer(db.ConnectionString);
        });

        // Repos + clock
        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddSingleton<ISystemClock, SystemClock>();

        services.AddAfhCalendarSdk(config);

        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICalendarSubscriptionStore, SqlCalendarSubscriptionStore>();
        return services;
    }
}
