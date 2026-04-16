namespace AFH.Acs.Domain;

public enum EndpointAccessPolicy
{
    Public,
    UserAuthenticated,
    InternalOnly,
    WebhookVerified
}
