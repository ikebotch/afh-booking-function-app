using AFH.Acs.Application.Abstractions.Identity;
using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Services.Identity;

public sealed class IdentityTokenService(IJoinTokenIssuer joinTokenIssuer) : IIdentityTokenService
{
    public Task<IssuedIdentityToken> IssueAsync(CancellationToken ct = default)
        => joinTokenIssuer.IssueIdentityTokenAsync(ct);
}
