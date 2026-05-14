namespace AFH.Booking.Domain;

public enum EndpointAccessPolicy
{
    Public,
    UserAuthenticated,
    InternalOnly,
    WebhookVerified
}
