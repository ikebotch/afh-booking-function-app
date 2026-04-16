using AFH.Acs.Function.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AFH.Acs.Function.Configuration;

public sealed class InternalApiAuthOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<InternalApiAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, InternalApiAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (hostEnvironment.IsDevelopment() && options.AllowAnonymousInDevelopment)
            return ValidateOptionsResult.Success;

        return string.IsNullOrWhiteSpace(options.Token)
            ? ValidateOptionsResult.Fail($"{InternalApiAuthOptions.SectionName}:Token is required unless anonymous development access is enabled.")
            : ValidateOptionsResult.Success;
    }
}
