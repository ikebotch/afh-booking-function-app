using Microsoft.Graph;

namespace AFH.Booking.Infrastructure.Composition;

public interface ISharePointGraphClient
{
    GraphServiceClient Client { get; }
}
