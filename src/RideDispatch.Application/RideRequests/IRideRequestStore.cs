using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Application.RideRequests;

public interface IRideRequestStore
{
    Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken);

    Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
