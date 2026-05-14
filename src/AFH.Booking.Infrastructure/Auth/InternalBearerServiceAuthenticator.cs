using System.Net.Http.Headers;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class InternalBearerServiceAuthenticator : IInternalServiceAuthenticator
{
    public void Apply(HttpRequestMessage request, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }
}
