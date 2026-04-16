using AFH.Acs.Application.Abstractions.Identity;
using AFH.Acs.Application.Services.Identity;
using Azure.Communication.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Acs.Infrastructure.Identity;

internal static class IdentityModuleServiceCollectionExtensions
{
    internal static IServiceCollection AddIdentityModule(this IServiceCollection services, string acsConnectionString)
    {
        services.AddSingleton(new CommunicationIdentityClient(acsConnectionString));
        services.AddScoped<IIdentityTokenService, IdentityTokenService>();

        return services;
    }
}
