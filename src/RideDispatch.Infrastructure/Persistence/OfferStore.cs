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
        dbContext.Offers
            .AsNoTracking()
            .SingleOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public Task<Offer?> FindByAllocationRunIdAsync(Guid allocationRunId, CancellationToken cancellationToken) =>
        dbContext.Offers
            .AsNoTracking()
            .SingleOrDefaultAsync(offer => offer.AllocationRunId == allocationRunId, cancellationToken);

    public Task<Offer?> FindActionableByRideRequestIdAsync(Guid rideRequestId, CancellationToken cancellationToken) =>
        dbContext.Offers.SingleOrDefaultAsync(
            offer => offer.RideRequestId == rideRequestId &&
                (offer.Status == OfferStatus.Pending || offer.Status == OfferStatus.Accepted),
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_offers_allocation_run_id" or "ux_offers_actionable_ride_request_id",
            })
        {
            dbContext.ChangeTracker.Clear();
            throw new OfferAlreadyExistsException(exception);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count > 0 && exception.Entries.All(entry => entry.Entity is Offer))
        {
            dbContext.ChangeTracker.Clear();
            throw new OfferStateConcurrencyException(exception);
        }
    }
}
