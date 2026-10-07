using Microsoft.EntityFrameworkCore;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class RideRequestStore(DispatchDbContext dbContext) : IRideRequestStore
{
    public async Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken) =>
        await dbContext.RideRequests.AddAsync(rideRequest, cancellationToken);

    public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.RideRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(rideRequest => rideRequest.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RideRequest>> ListAsync(
        CancellationToken cancellationToken) =>
        await dbContext.RideRequests
            .AsNoTracking()
            .OrderByDescending(rideRequest => rideRequest.CreatedAt)
            .ThenByDescending(rideRequest => rideRequest.Id)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
