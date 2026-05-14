using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Bookings.Scoring;
using AFH.Booking.Application.Calendar.Handlers;

namespace AFH.Booking.Application.Availability;

internal static class AvailabilityModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingAvailabilityModule(this IServiceCollection services)
    {
        services.AddSingleton(new ScoreWeights());
        services.AddSingleton<ISlotScorer, SlotScorer>();
        services.AddScoped<AvailabilityHandler>();
        services.AddScoped<IAvailabilityHandler, AvailabilityHandler>();

        services.AddScoped<IProspectResolver, ProspectResolver>();
        services.AddScoped<IAvailabilityTransactionGuard, AvailabilityTransactionGuard>();
        services.AddScoped<ISlotStartBuilder, SlotStartBuilder>();
        services.AddScoped<IAdviserPoolBuilder, AdviserPoolBuilder>();
        services.AddScoped<IAvailabilitySlotProcessor, AvailabilitySlotProcessor>();
        services.AddScoped<IAvailabilityResponseBuilder, AvailabilityResponseBuilder>();
        services.AddScoped<ICalendarViewQueryHandler, CalendarViewQueryHandler>();

        return services;
    }
}
