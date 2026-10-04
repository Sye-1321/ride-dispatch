namespace RideDispatch.Application.DriverLocations;

public interface IDriverLocationStore
{
    Task<DriverLocationView> UpsertAsync(
        Guid driverId,
        double latitude,
        double longitude,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken);

    Task<DriverLocationView?> FindByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NearbyDriverView>> FindWithinRadiusAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken);
}
