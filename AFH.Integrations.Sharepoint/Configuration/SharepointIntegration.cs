using AFH.Integrations.Sharepoint.Connector;
using AFH.Integrations.Sharepoint.Services;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace AFH.Integrations.Sharepoint.Configuration
{
	public static class SharepointIntegration
	{
		public static IServiceCollection Register(this IServiceCollection services, IConfiguration configuration)
		{
			AzureAdConfig graphConfig = configuration.GetSection("AzureAD").Get<AzureAdConfig>();
			

            services.AddSingleton<GraphServiceClient>(sp =>
						{
							return new GraphServiceClient
									(
										new ClientSecretCredential
										(
											graphConfig.TenantId,
											graphConfig.ClientId, graphConfig.ClientSecret,
											new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud }
										),
										new[] { graphConfig.Scope }
									);
						});

			services.AddSingleton<SharepointConnector>();
			services.AddSingleton<SharepointService>();

			return services;

		}
	}
}
