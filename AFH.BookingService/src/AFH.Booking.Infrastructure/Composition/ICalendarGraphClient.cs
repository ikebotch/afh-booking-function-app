using Microsoft.Graph;

namespace AFH.Booking.Infrastructure.Composition;

public interface ICalendarGraphClient
{
    GraphServiceClient Client { get; }
}