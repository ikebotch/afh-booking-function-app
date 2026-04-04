using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Bookings;

namespace AFH.Booking.Application.Composition;

internal static class HoldsModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingHoldsModule(this IServiceCollection services)
    {
        services.AddScoped<ICreateBookingHandler, CreateBookingHandler>();
        services.AddScoped<IConfirmBookingHandler, ConfirmBookingHandler>();
        services.AddScoped<IReleaseHoldHandler, ReleaseHoldHandler>();

        return services;
    }
}
