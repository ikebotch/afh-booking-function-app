using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Bookings.Scoring;
using AFH.Booking.Application.Calendar.Queries;

namespace AFH.Booking.Application.Composition;

internal static class AvailabilityModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingAvailabilityModule(this IServiceCollection services)
    {
        services.AddSingleton(new ScoreWeights());
        services.AddSingleton<ISlotScorer, SlotScorer>();
        services.AddScoped<AvailabilityHandler>();
        services.AddScoped<IAvailabilityHandler, AvailabilityHandler>();
        services.AddScoped<ICalendarViewQueryHandler, CalendarViewQueryHandler>();
        services.AddScoped<IReleaseHoldHandler, ReleaseHoldHandler>();

        return services;
    }
}
