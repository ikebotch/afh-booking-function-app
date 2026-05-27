using AFH.Notification.Application.Abstractions;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Delivery.Email;

public sealed class ContactCentreRoutingResolver : IContactCentreRoutingResolver
{
    private readonly EmailDeliveryOptions _emailOptions;

    public ContactCentreRoutingResolver(IOptions<EmailDeliveryOptions> emailOptions)
    {
        _emailOptions = emailOptions.Value;
    }

    public string? GetContactCentreEmailAddress()
    {
        return _emailOptions.ContactCentreEmailAddress;
    }
}
