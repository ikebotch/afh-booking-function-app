using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Models.Lifecycle;

public sealed record BookingLifecycleEventRecord(
    string BookingId,
    string? TransactionId,
    string EventType,
    BookingActorContext? ActorContext,
    string? ActorType,
    string? ActorId,
    string? ReasonCode,
    string? ReasonNotes,
    object? Before,
    object? After,
    DateTime OccurredUtc,
    string? CorrelationId,
    string? SourceSystem = "BookingService",
    string? RelatedBookingId = null,
    string? PreviousState = null,
    string? NewState = null,
    string? TriggerReason = null,
    string? PartnerName = null);

public sealed record BookingLifecycleStepRecord(
    string StepName,
    int Sequence,
    string Status,
    DateTime StartedUtc,
    DateTime? CompletedUtc = null,
    string? ErrorCode = null,
    string? ErrorDetails = null,
    string? CorrelationId = null,
    BookingActorContext? ActorContext = null);
