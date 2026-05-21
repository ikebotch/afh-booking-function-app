using AFH.Booking.Application.Holds;

namespace AFH.Booking.Application.Composition;

internal static class HoldsModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingHoldsModule(this IServiceCollection services)
    {
        services.AddScoped<ICreateBookingService, CreateBookingService>();
        services.AddScoped<IConfirmBookingService, ConfirmBookingService>();
        services.AddScoped<IReleaseHoldService, ReleaseHoldService>();

        return services;
    }
}
