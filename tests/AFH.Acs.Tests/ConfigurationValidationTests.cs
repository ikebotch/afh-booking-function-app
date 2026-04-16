using AFH.Acs.Function.Configuration;
using AFH.Acs.Function.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AFH.Acs.Tests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void InternalApiAuthValidator_RequiresTokenOutsideAnonymousDevelopment()
    {
        var validator = new InternalApiAuthOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(null, new InternalApiAuthOptions());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void InternalApiAuthValidator_AllowsAnonymousDevelopmentWithoutToken()
    {
        var validator = new InternalApiAuthOptionsValidator(new FakeHostEnvironment(Environments.Development));

        var result = validator.Validate(null, new InternalApiAuthOptions
        {
            AllowAnonymousInDevelopment = true
        });

        Assert.True(result.Succeeded);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AFH.Acs.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
