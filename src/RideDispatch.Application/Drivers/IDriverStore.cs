using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public interface IDriverStore
{
    Task AddAsync(Driver driver, CancellationToken cancellationToken);

    Task<Driver?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Driver>> ListAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class DriverStateConcurrencyException(Exception innerException)
    : Exception("The driver state was changed concurrently.", innerException);

