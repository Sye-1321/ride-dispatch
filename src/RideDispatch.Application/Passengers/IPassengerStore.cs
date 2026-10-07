using RideDispatch.Domain.Passengers;

namespace RideDispatch.Application.Passengers;

public interface IPassengerStore
{
    Task AddAsync(Passenger passenger, CancellationToken cancellationToken);

    Task<Passenger?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
