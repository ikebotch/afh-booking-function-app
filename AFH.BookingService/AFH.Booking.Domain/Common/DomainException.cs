namespace AFH.Booking.Domain.Common;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
