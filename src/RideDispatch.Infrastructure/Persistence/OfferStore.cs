using Microsoft.EntityFrameworkCore;
using Npgsql;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Infrastructure.Persistence;

public sealed class OfferStore(DispatchDbContext dbContext) : IOfferStore
{
    public async Task AddAsync(Offer offer, CancellationToken cancellationToken) =>
        await dbContext.Offers.AddAsync(offer, cancellationToken);

    public Task<Offer?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Offers.SingleOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Offers.AsNoTracking().SingleOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public Task<Offer?> FindByAllocationRunIdAsync(Guid allocationRunId, CancellationToken cancellationToken) =>
        dbContext.Offers.AsNoTracking().SingleOrDefaultAsync(offer => offer.AllocationRunId == allocationRunId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_offers_allocation_run_id" })
        {
            throw new OfferAlreadyExistsException(exception);
        }
    }
}
