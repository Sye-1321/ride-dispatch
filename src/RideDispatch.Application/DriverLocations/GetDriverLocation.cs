namespace RideDispatch.Application.DriverLocations;

public sealed class GetDriverLocation(IDriverLocationStore locationStore)
{
    public Task<DriverLocationView?> ExecuteAsync(
        Guid driverId,
        CancellationToken cancellationToken) =>
        locationStore.FindByDriverIdAsync(driverId, cancellationToken);
}
