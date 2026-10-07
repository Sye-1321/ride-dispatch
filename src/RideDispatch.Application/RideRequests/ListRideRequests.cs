namespace RideDispatch.Application.RideRequests;

public sealed class ListRideRequests(IRideRequestStore rideRequestStore)
{
    public async Task<IReadOnlyList<RideRequestView>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        var rideRequests = await rideRequestStore.ListAsync(cancellationToken);
        return rideRequests.Select(RideRequestView.FromRideRequest).ToList();
    }
}
