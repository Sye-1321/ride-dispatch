using Microsoft.EntityFrameworkCore;
using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class DriverStore(DispatchDbContext dbContext) : IDriverStore
{
    public async Task AddAsync(Driver driver, CancellationToken cancellationToken) =>
        await dbContext.Drivers.AddAsync(driver, cancellationToken);

    public Task<Driver?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Drivers.SingleOrDefaultAsync(driver => driver.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Driver>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Drivers
            .AsNoTracking()
            .OrderBy(driver => driver.Name)
            .ThenBy(driver => driver.Id)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count > 0 && exception.Entries.All(entry => entry.Entity is Driver))
        {
            dbContext.ChangeTracker.Clear();
            throw new DriverStateConcurrencyException(exception);
        }
    }
}
