namespace AFH.Booking.Application.Holds;

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
