namespace RideDispatch.Application.RideRequests;

public sealed class GetRideRequest(IRideRequestStore rideRequestStore)
{
    public async Task<RideRequestView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rideRequest = await rideRequestStore.FindByIdAsync(id, cancellationToken);
        return rideRequest is null ? null : RideRequestView.FromRideRequest(rideRequest);
    }
}
