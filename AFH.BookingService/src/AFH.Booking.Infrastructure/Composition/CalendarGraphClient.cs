using Microsoft.Graph;

namespace AFH.Booking.Infrastructure.Composition;
public sealed class CalendarGraphClient : ICalendarGraphClient
{
    public GraphServiceClient Client { get; }
    public CalendarGraphClient(GraphServiceClient client) => Client = client;
}
