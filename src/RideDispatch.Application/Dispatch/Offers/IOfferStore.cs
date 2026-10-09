using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Offers;

public interface IOfferStore
{
    Task AddAsync(Offer offer, CancellationToken cancellationToken);
    Task<Offer?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);
    Task<Offer?> FindByAllocationRunIdAsync(Guid allocationRunId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class OfferAlreadyExistsException : Exception
{
    public OfferAlreadyExistsException(Exception innerException)
        : base("An offer already exists for the allocation run.", innerException)
    {
    }
}
