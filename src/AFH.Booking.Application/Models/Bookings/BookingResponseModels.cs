using AFH.Booking.Application.Models.Availability;

namespace AFH.Booking.Application.Models.Bookings;

public sealed class CreateBookingResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string SlotId { get; init; } = default!;
    public DateTime HoldExpiresUtc { get; init; }
    public int CompanyBufferMinutes { get; init; }
}

public sealed class ConfirmBookingResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string SlotId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string TransactionRef { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string LifecycleState { get; init; } = default!;
    public string? OnlineMeetingJoinUrl { get; init; }
}

public sealed class ReleaseHoldResponse
{
    public string? Success { get; init; }
    public string? BookingId { get; init; }
    public ReleaseHoldError? Error { get; init; }
}

public sealed class ReleaseHoldError
{
    public string? Code { get; init; }
    public string? Message { get; init; }
}

public sealed class CancelBookingResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public DateTime CancelledUtc { get; init; }
    public string Status { get; init; } = "Cancelled";
}

public sealed class BookingDetailsResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string SlotId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string TransactionRef { get; init; } = default!;
    public string AdviserId { get; init; } = default!;
    public string AdviserName { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public int DurationMinutes { get; init; }
    public bool IsRemote { get; init; }
    public string? MeetingType { get; init; }
    public string Status { get; init; } = default!;
    public DateTime? ConfirmedUtc { get; init; }
    public DateTime? CancelledUtc { get; init; }
    public string? CancelReason { get; init; }
    public string? ViewBookingUrl { get; init; }
    public string? CancelBookingUrl { get; init; }
    public string? RescheduleBookingUrl { get; init; }
}

public sealed class AdminBookingSearchResponse
{
    public IReadOnlyList<AdminBookingSearchItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class AdminBookingSearchItem
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string SlotId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string TransactionRef { get; init; } = default!;
    public string ClientRef { get; init; } = default!;
    public string AdviserId { get; init; } = default!;
    public string AdviserName { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public int DurationMinutes { get; init; }
    public bool IsRemote { get; init; }
    public string? MeetingType { get; init; }
    public string? LocationRef { get; init; }
    public string Status { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public DateTime? ConfirmedUtc { get; init; }
    public DateTime? CancelledUtc { get; init; }
    public string? CancelReason { get; init; }
}

public sealed class RearrangeBookingResponse
{
    public string PreviousBookingId { get; init; } = default!;
    public string? PreviousBookingReference { get; init; }
    public string NewBookingId { get; init; } = default!;
    public string? NewBookingReference { get; init; }
    public string NewSlotId { get; init; } = default!;
    public string PreviousAdviserId { get; init; } = default!;
    public string PreviousAdviserName { get; init; } = default!;
    public DateTime PreviousStartUtc { get; init; }
    public DateTime PreviousEndUtc { get; init; }
    public string NewAdviserId { get; init; } = default!;
    public string NewAdviserName { get; init; } = default!;
    public DateTime NewStartUtc { get; init; }
    public DateTime NewEndUtc { get; init; }
    public string NotificationSummary { get; init; } = default!;
}

public sealed class RearrangementOptionsResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string TransactionId { get; init; } = default!;
    public string AssignedAdviserId { get; init; } = default!;
    public string AssignedAdviserName { get; init; } = default!;
    public bool AssignedAdviserHasAvailability { get; init; }
    public GetAvailabilityResponse AssignedAdviserOptions { get; init; } = new();
    public GetAvailabilityResponse AlternativeAdviserOptions { get; init; } = new();
}

public sealed class RecordNoShowResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string TransactionId { get; init; } = default!;
    public string LifecycleEventId { get; init; } = default!;
    public string PreviousState { get; init; } = default!;
    public string NewState { get; init; } = default!;
    public DateTime RecordedUtc { get; init; }
}

public sealed record BookingChangeActorContext(
    string ActorType,
    string? ActorId,
    string? TransactionRef,
    string? CorrelationId = null);
