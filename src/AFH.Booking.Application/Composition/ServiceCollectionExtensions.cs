using AFH.Booking.Application.Abstractions;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Bookings.Scoring;
using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Application.Calendar.Subscriptions;
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
        services.AddScoped<ICalendarGovernanceService, CalendarGovernanceService>();
        services.AddScoped<ILifecycleAuditService, LifecycleAuditService>();
        services.AddScoped<ProcessNotificationsHandler>();

        // Availability
        services.AddSingleton(new ScoreWeights());
        services.AddSingleton<ISlotScorer, SlotScorer>();
        services.AddScoped<AvailabilityHandler>();



        services.AddScoped<IAvailabilityHandler, AvailabilityHandler>();
        services.AddScoped<ICalendarViewQueryHandler, CalendarViewQueryHandler>();


        
        services.AddScoped<ICreateSubscriptionHandler, CreateSubscriptionHandler>();
        services.AddScoped<IProcessNotificationsHandler, ProcessNotificationsHandler>();
        services.AddScoped<IReleaseHoldHandler, ReleaseHoldHandler>();

    


        return services;
    }
}
