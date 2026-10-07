namespace RideDispatch.Application.Passengers;

public sealed class GetPassenger(IPassengerStore passengerStore)
{
    public async Task<PassengerView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var passenger = await passengerStore.FindByIdAsync(id, cancellationToken);
        return passenger is null ? null : PassengerView.FromPassenger(passenger);
    }
}
