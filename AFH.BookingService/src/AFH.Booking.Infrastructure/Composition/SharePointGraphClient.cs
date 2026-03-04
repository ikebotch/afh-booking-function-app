using Microsoft.Graph;

namespace AFH.Booking.Infrastructure.Composition;
public sealed class SharePointGraphClient : ISharePointGraphClient
{
    public GraphServiceClient Client { get; }
    public SharePointGraphClient(GraphServiceClient client) => Client = client;
}


