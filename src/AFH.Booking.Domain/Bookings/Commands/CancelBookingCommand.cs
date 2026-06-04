namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class CancelBookingCommand
{
    private string? _requestedBy;
    private string? _actorId;
    private string? _correlationId;

    public string BookingId { get; set; } = default!;
    public string? Reason { get; set; }
    public BookingActorContext? ActorContext { get; set; }
    public string? RequestedBy
    {
        get => ActorContext?.ActorType ?? _requestedBy;
        set => _requestedBy = value;
    }
    public string? ActorId
    {
        get => ActorContext?.ActorId ?? _actorId;
        set => _actorId = value;
    }
    public string? ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public string? ApprovalRequestId { get; set; }
    public string? CorrelationId
    {
        get => ActorContext?.CorrelationId ?? _correlationId;
        set => _correlationId = value;
    }
}
