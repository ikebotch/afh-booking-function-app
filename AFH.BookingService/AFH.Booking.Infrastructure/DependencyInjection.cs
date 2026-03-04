using AFH.Booking.Infrastructure.Calendar;
using AFH.Booking.Infrastructure.Clock;
using AFH.Booking.Infrastructure.Options;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Repositories;

namespace AFH.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddOptions<BookingDbOptions>()
            .Bind(config.GetSection(BookingDbOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "BookingDb:ConnectionString is required.")
            .ValidateOnStart();

        services.AddOptions<CalendarServiceOptions>()
            .Bind(config.GetSection(CalendarServiceOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient(nameof(CalendarService));

        services.AddDbContext<BookingDbContext>((sp, opts) =>
        {
            var db = sp.GetRequiredService<IOptions<BookingDbOptions>>().Value;
            opts.UseSqlServer(db.ConnectionString);
        });

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<ICalendarService, CalendarService>();

        return services;
    }
}
