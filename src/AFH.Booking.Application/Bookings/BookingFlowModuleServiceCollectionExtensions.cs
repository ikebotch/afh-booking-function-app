using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Governance;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Lifecycle;

namespace AFH.Booking.Application.Composition;

internal static class BookingFlowModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingFlowModule(this IServiceCollection services)
    {
        services.AddScoped<ICancelBookingHandler, CancelBookingHandler>();
        services.AddScoped<IBookingDetailsHandler, BookingDetailsHandler>();
        services.AddScoped<INoShowBookingHandler, NoShowBookingHandler>();
        services.AddScoped<IRearrangementOptionsHandler, RearrangementOptionsHandler>();
        services.AddScoped<IRearrangeBookingHandler, RearrangeBookingHandler>();
        services.AddScoped<ICancellationOrchestrator, CancellationOrchestrator>();
        services.AddScoped<IRearrangementOrchestrator, RearrangementOrchestrator>();
        services.AddScoped<IBookingConflictService, BookingConflictService>();
        services.AddScoped<ISelectedSlotRouteTimeGuard, SelectedSlotRouteTimeGuard>();
        services.AddScoped<ILifecycleAuditService, LifecycleAuditService>();
        services.AddScoped<IAvailabilityRulesService, AvailabilityRulesService>();

        return services;
    }
}
