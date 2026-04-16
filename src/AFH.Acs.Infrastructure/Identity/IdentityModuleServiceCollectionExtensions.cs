using AFH.Acs.Application.Abstractions;
using AFH.Acs.Application.Services;
using AFH.Acs.Infrastructure.Acs;
using Azure.Communication.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Acs.Infrastructure.Extensions;

internal static class IdentityModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddIdentityModule(this IServiceCollection services, string acsConnectionString)
    {
        services.AddSingleton(new CommunicationIdentityClient(acsConnectionString));
        services.AddScoped<IIdentityTokenService, IdentityTokenService>();

        return services;
    }
}
