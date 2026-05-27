using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Abstractions;

public interface IContactCentreRoutingResolver
{
    string? GetContactCentreEmailAddress();
}
