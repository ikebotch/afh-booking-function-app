using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions;

public interface IIdentityTokenService
{
    Task<IssuedIdentityToken> IssueAsync(CancellationToken ct = default);
}
