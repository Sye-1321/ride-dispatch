using Microsoft.EntityFrameworkCore;
using RideDispatch.Application.Vehicles;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class VehicleStore(DispatchDbContext dbContext) : IVehicleStore
{
    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken) =>
        await dbContext.Vehicles.AddAsync(vehicle, cancellationToken);

    public Task<Vehicle?> FindByDriverIdAsync(Guid driverId, CancellationToken cancellationToken) =>
        dbContext.Vehicles
            .AsNoTracking()
            .SingleOrDefaultAsync(vehicle => vehicle.DriverId == driverId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
