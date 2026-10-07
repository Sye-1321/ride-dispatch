using Microsoft.EntityFrameworkCore;
using RideDispatch.Application.Passengers;
using RideDispatch.Domain.Passengers;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class PassengerStore(DispatchDbContext dbContext) : IPassengerStore
{
    public async Task AddAsync(Passenger passenger, CancellationToken cancellationToken) =>
        await dbContext.Passengers.AddAsync(passenger, cancellationToken);

    public Task<Passenger?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Passengers
            .AsNoTracking()
            .SingleOrDefaultAsync(passenger => passenger.Id == id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
