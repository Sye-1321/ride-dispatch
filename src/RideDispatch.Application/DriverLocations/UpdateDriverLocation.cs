using RideDispatch.Application.Drivers;

namespace RideDispatch.Application.DriverLocations;

public enum UpdateDriverLocationOutcome
{
    Success,
    DriverNotFound,
}

public sealed record UpdateDriverLocationResult(
    UpdateDriverLocationOutcome Outcome,
    DriverLocationView? Location = null);

public sealed class UpdateDriverLocation(
    IDriverStore driverStore,
    IDriverLocationStore locationStore,
    TimeProvider timeProvider)
{
    public async Task<UpdateDriverLocationResult> ExecuteAsync(
        Guid driverId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (await driverStore.FindAsync(driverId, cancellationToken) is null)
        {
            return new(UpdateDriverLocationOutcome.DriverNotFound);
        }

        LocationValidation.ValidateCoordinates(latitude, longitude);

        var location = await locationStore.UpsertAsync(
            driverId,
            latitude,
            longitude,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return new(UpdateDriverLocationOutcome.Success, location);
    }
}
