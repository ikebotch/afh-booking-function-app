using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Approvals;
using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Governance;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Lifecycle;
using AFH.Booking.Application.Services.Bookings.Core;
using AFH.Booking.Application.Services.Lifecycle;

namespace AFH.Booking.Application.Composition;

internal static class BookingFlowModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingFlowModule(this IServiceCollection services)
    {
        services.AddScoped<IConfirmBookingService, ConfirmBookingService>();
        services.AddScoped<IBookingTokenService, BookingTokenService>();
        services.AddScoped<ICancelBookingService, CancelBookingService>();
        services.AddScoped<IBookingDetailsService, BookingDetailsService>();
        services.AddScoped<INoShowBookingService, NoShowBookingService>();
        services.AddScoped<IRearrangementOptionsService, RearrangementOptionsService>();
        services.AddScoped<IRearrangeBookingService, RearrangeBookingService>();
        services.AddScoped<ICancellationOrchestrator, CancellationOrchestrator>();
        services.AddScoped<IRearrangementOrchestrator, RearrangementOrchestrator>();
        services.AddScoped<IBookingConflictService, BookingConflictService>();
        services.AddScoped<ISelectedSlotRouteTimeGuard, SelectedSlotRouteTimeGuard>();
        services.AddScoped<ILifecycleAuditService, LifecycleAuditService>();
        services.AddScoped<IAvailabilityRulesService, AvailabilityRulesService>();
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddScoped<IBookingNotificationStep, BookingNotificationStep>();

        return services;
    }
}
