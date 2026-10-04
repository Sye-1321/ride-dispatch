using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.Vehicles;

public interface IVehicleStore
{
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task<Vehicle?> FindByDriverIdAsync(Guid driverId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
