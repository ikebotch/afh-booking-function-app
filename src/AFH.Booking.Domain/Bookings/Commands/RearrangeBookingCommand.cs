namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class RearrangeBookingCommand
{
    private string? _requestedBy;
    private string? _actorId;
    private string? _correlationId;

    public string BookingId { get; init; } = default!;
    public string NewSlotId { get; init; } = default!;
    public BookingActorContext? ActorContext { get; init; }
    public string RequestedBy
    {
        get => ActorContext?.ActorType ?? _requestedBy ?? BookingActorContext.ActorClient;
        init => _requestedBy = value;
    }
    public string? ActorId
    {
        get => ActorContext?.ActorId ?? _actorId;
        init => _actorId = value;
    }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public string? ApprovalRequestId { get; init; }
    public string? CorrelationId
    {
        get => ActorContext?.CorrelationId ?? _correlationId;
        init => _correlationId = value;
    }
}
