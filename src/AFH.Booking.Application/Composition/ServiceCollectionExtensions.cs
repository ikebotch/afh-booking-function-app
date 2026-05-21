using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Holds;

namespace AFH.Booking.Application.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookingApplication(this IServiceCollection services)
    {
        // Clock
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITimeZoneProvider, DefaultTimeZoneProvider>();

        services.AddBookingFlowModule();
        services.AddBookingHoldsModule();
        services.AddBookingAvailabilityModule();

        // Booking flow
        services.AddScoped<ICreateBookingService, CreateBookingService>();

        // Refactored services
        services.AddScoped<IBookingContextLoader, BookingContextLoader>();
        services.AddScoped<IBookingHoldService, BookingHoldService>();
        services.AddScoped<IBookingCalendarService, BookingCalendarService>();
        services.AddScoped<IHoldWindowFactory, HoldWindowFactory>();

        return services;
    }
}