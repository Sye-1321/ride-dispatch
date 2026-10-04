using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.Vehicles;

public sealed record VehicleView(
    Guid Id,
    Guid DriverId,
    VehicleType Type,
    string PlateNumber)
{
    public static VehicleView FromVehicle(Vehicle vehicle) =>
        new(vehicle.Id, vehicle.DriverId, vehicle.Type, vehicle.PlateNumber);
}
