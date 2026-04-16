using AFH.Acs.Application.Abstractions;
using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Services;

public sealed class IdentityTokenService(IJoinTokenIssuer joinTokenIssuer) : IIdentityTokenService
{
    public Task<IssuedIdentityToken> IssueAsync(CancellationToken ct = default)
        => joinTokenIssuer.IssueIdentityTokenAsync(ct);
}
