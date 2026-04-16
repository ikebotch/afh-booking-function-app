using AFH.Acs.Domain;

namespace AFH.Acs.Function.Security;

public static class EndpointAccessPolicies
{
    private static readonly IReadOnlyDictionary<string, EndpointAccessPolicy> Policies =
        new Dictionary<string, EndpointAccessPolicy>(StringComparer.Ordinal)
        {
            ["v1-health"] = EndpointAccessPolicy.Public,
            ["v1-openapi-json"] = EndpointAccessPolicy.Public,
            ["v1-scalar-ui"] = EndpointAccessPolicy.Public,
            ["v1-meetings-create"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-get-by-id"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-get-by-group"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-consent"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-join-token"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-identity-token"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-link"] = EndpointAccessPolicy.InternalOnly,
            ["v1-recordings-start"] = EndpointAccessPolicy.InternalOnly,
            ["v1-recordings-stop"] = EndpointAccessPolicy.InternalOnly,
            ["v1-recordings-list"] = EndpointAccessPolicy.InternalOnly,
            ["v1-recordings-get"] = EndpointAccessPolicy.InternalOnly,
            ["v1-meetings-transcriptions-submit"] = EndpointAccessPolicy.InternalOnly,
            ["v1-transcriptions-status"] = EndpointAccessPolicy.InternalOnly,
            ["v1-transcriptions-files"] = EndpointAccessPolicy.InternalOnly,
            ["v1-transcriptions-content"] = EndpointAccessPolicy.InternalOnly,
            ["v1-transcriptions-speaker-content"] = EndpointAccessPolicy.InternalOnly,
            ["v1-transcriptions-cancel"] = EndpointAccessPolicy.InternalOnly,
            ["v1-transcriptions-delete"] = EndpointAccessPolicy.InternalOnly
        };

    internal static IReadOnlyCollection<string> KnownHttpFunctions => Policies.Keys.ToArray();

    public static EndpointAccessPolicy GetPolicy(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        if (Policies.TryGetValue(functionName, out var policy))
            return policy;

        throw new InvalidOperationException($"No endpoint access policy is configured for HTTP function '{functionName}'.");
    }
}
