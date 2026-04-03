using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Governance;
using AFH.Booking.Application.Lifecycle;
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

        // Booking handlers
        services.AddScoped<ICreateBookingHandler, CreateBookingHandler>();
        services.AddScoped<IConfirmBookingHandler, ConfirmBookingHandler>();
        services.AddScoped<ICancelBookingHandler, CancelBookingHandler>();
        services.AddScoped<IBookingDetailsHandler, BookingDetailsHandler>();
        services.AddScoped<IRearrangementOptionsHandler, RearrangementOptionsHandler>();
        services.AddScoped<IRearrangeBookingHandler, RearrangeBookingHandler>();
        services.AddScoped<ICancellationOrchestrator, CancellationOrchestrator>();
        services.AddScoped<IRearrangementOrchestrator, RearrangementOrchestrator>();
        services.AddScoped<IBookingConflictService, BookingConflictService>();
        services.AddScoped<ILifecycleAuditService, LifecycleAuditService>();
        services.AddBookingAvailabilityModule();

        return services;
    }
}
