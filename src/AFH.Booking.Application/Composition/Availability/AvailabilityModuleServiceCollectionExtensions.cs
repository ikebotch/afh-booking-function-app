using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Bookings.Scoring;
using AFH.Booking.Application.Calendar;

namespace AFH.Booking.Application.Composition;

internal static class AvailabilityModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingAvailabilityModule(this IServiceCollection services)
    {
        services.AddSingleton(new ScoreWeights());
        services.AddSingleton<ISlotScorer, SlotScorer>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();

        services.AddScoped<IProspectResolver, ProspectResolver>();
        services.AddScoped<IAvailabilityTransactionGuard, AvailabilityTransactionGuard>();
        services.AddScoped<ISlotStartBuilder, SlotStartBuilder>();
        services.AddScoped<IAdviserPoolBuilder, AdviserPoolBuilder>();
        services.AddScoped<IAvailabilitySlotProcessor, AvailabilitySlotProcessor>();
        services.AddScoped<IAvailabilityResponseBuilder, AvailabilityResponseBuilder>();
        services.AddScoped<ICalendarViewQueryService, CalendarViewQueryService>();

        return services;
    }
}
