namespace AFH.Booking.Application.Holds;

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
