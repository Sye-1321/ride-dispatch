namespace RideDispatch.Application.Vehicles;

public sealed class GetDriverVehicle(IVehicleStore vehicleStore)
{
    public async Task<VehicleView?> ExecuteAsync(Guid driverId, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleStore.FindByDriverIdAsync(driverId, cancellationToken);
        return vehicle is null ? null : VehicleView.FromVehicle(vehicle);
    }
}
