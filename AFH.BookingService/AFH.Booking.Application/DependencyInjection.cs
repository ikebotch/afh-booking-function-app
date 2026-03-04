using AFH.Booking.Application.Bookings.Handlers;
using AFH.Booking.Application.Calendar.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Booking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingApplication(this IServiceCollection services, 
        IConfiguration config)
    {
        services.AddScoped<ICreateHoldHandler, CreateHoldHandler>();
        services.AddScoped<IConfirmBookingHandler, ConfirmBookingHandler>();
        services.AddScoped<ICancelBookingHandler, CancelBookingHandler>();

        services.AddScoped<ICalendarViewHandler, CalendarViewHandler>();
        services.AddScoped<IGetScheduleHandler, GetScheduleHandler>();

        return services;
    }
}
