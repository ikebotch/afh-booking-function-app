using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions.Identity;

public interface IIdentityTokenService
{
    Task<IssuedIdentityToken> IssueAsync(CancellationToken ct = default);
}
