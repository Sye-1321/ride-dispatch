using RideDispatch.Domain.Passengers;

namespace RideDispatch.Application.Passengers;

public sealed record PassengerView(Guid Id, string Name, string PhoneNumber)
{
    public static PassengerView FromPassenger(Passenger passenger) =>
        new(passenger.Id, passenger.Name, passenger.PhoneNumber);
}
