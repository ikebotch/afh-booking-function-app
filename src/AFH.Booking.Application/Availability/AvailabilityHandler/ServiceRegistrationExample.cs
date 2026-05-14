using AFH.Booking.Application.Abstractions.Availability;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Booking.Application.Availability;

public static class AvailabilityServiceRegistrationExample
{
    public static IServiceCollection AddAvailabilityRefactorServices(this IServiceCollection services)
    {
        services.AddScoped<IProspectResolver, ProspectResolver>();
        services.AddScoped<IAvailabilityTransactionGuard, AvailabilityTransactionGuard>();
        services.AddScoped<ISlotStartBuilder, SlotStartBuilder>();
        services.AddScoped<IAdviserPoolBuilder, AdviserPoolBuilder>();
        services.AddScoped<IAvailabilitySlotProcessor, AvailabilitySlotProcessor>();
        services.AddScoped<IAvailabilityResponseBuilder, AvailabilityResponseBuilder>();

        return services;
    }
}
