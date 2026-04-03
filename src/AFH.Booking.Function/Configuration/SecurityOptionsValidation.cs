using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Function.Configuration;

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

public sealed class DomainUserAuthOptionsValidator : IValidateOptions<DomainUserAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, DomainUserAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.Audience))
            return ValidateOptionsResult.Fail($"{DomainUserAuthOptions.SectionName}:Audience is required when domain user authentication is enabled.");

        if (string.IsNullOrWhiteSpace(options.Authority) && string.IsNullOrWhiteSpace(options.TenantId))
            return ValidateOptionsResult.Fail($"{DomainUserAuthOptions.SectionName}:Authority or {DomainUserAuthOptions.SectionName}:TenantId is required when domain user authentication is enabled.");

        return ValidateOptionsResult.Success;
    }
}
