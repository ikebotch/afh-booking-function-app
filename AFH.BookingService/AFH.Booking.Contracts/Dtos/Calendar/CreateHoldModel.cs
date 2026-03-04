using AFH.Booking.Contracts.Requests;

namespace AFH.Booking.Application.Bookings.Commands
{
    public sealed class CreateHoldModel
    {
        public CreateHoldRequest Request { get; }
        public string IdempotencyKey { get; }

        public CreateHoldModel(CreateHoldRequest request, string idempotencyKey)
        {
            Request = request;
            IdempotencyKey = idempotencyKey;
        }
    }
}
