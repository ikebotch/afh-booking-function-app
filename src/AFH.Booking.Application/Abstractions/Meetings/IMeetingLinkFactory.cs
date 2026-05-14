namespace AFH.Booking.Application.Abstractions.Meetings;

public interface IMeetingLinkFactory
{
    Task<string?> CreateJoinLinkAsync(string bookingId, CancellationToken ct);
}
