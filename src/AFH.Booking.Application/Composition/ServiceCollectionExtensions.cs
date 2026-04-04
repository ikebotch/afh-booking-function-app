using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;

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

        return services;
    }
}
