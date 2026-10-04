using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.Vehicles;

public enum RegisterVehicleOutcome
{
    Success,
    DriverNotFound,
    VehicleAlreadyRegistered,
}

public sealed record RegisterVehicleResult(
    RegisterVehicleOutcome Outcome,
    VehicleView? Vehicle = null);

public sealed class RegisterVehicle(IDriverStore driverStore, IVehicleStore vehicleStore)
{
    public async Task<RegisterVehicleResult> ExecuteAsync(
        Guid driverId,
        VehicleType type,
        string plateNumber,
        CancellationToken cancellationToken)
    {
        if (await driverStore.FindAsync(driverId, cancellationToken) is null)
        {
            return new(RegisterVehicleOutcome.DriverNotFound);
        }

        if (await vehicleStore.FindByDriverIdAsync(driverId, cancellationToken) is not null)
        {
            return new(RegisterVehicleOutcome.VehicleAlreadyRegistered);
        }

        var vehicle = Vehicle.Create(driverId, type, plateNumber);
        await vehicleStore.AddAsync(vehicle, cancellationToken);
        await vehicleStore.SaveChangesAsync(cancellationToken);

        return new(RegisterVehicleOutcome.Success, VehicleView.FromVehicle(vehicle));
    }
}
