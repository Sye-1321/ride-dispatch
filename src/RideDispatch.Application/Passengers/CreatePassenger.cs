using RideDispatch.Domain.Passengers;

namespace RideDispatch.Application.Passengers;

public sealed class CreatePassenger(IPassengerStore passengerStore)
{
    public async Task<PassengerView> ExecuteAsync(
        string name,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        var passenger = Passenger.Create(name, phoneNumber);
        await passengerStore.AddAsync(passenger, cancellationToken);
        await passengerStore.SaveChangesAsync(cancellationToken);
        return PassengerView.FromPassenger(passenger);
    }
}
