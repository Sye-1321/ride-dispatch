namespace RideDispatch.Application.DriverLocations;

public sealed class FindNearbyDrivers(IDriverLocationStore locationStore)
{
    public Task<IReadOnlyList<NearbyDriverView>> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken)
    {
        LocationValidation.ValidateCoordinates(latitude, longitude);
        LocationValidation.ValidateRadius(radiusMeters);

        return locationStore.FindWithinRadiusAsync(
            latitude,
            longitude,
            radiusMeters,
            cancellationToken);
    }
}
