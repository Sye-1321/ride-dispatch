using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Offers;

public enum CreateOfferOutcome { Success, AllocationRunNotFound, NoRecommendedDriver, OfferAlreadyExists }

public sealed record CreateOfferResult(CreateOfferOutcome Outcome, OfferView? Offer = null);

public sealed class CreateOffer(
    IAllocationRunStore allocationRunStore,
    IOfferStore offerStore,
    OfferPolicy policy,
    TimeProvider timeProvider)
{
    public async Task<CreateOfferResult> ExecuteAsync(Guid allocationRunId, CancellationToken cancellationToken)
    {
        var allocationRun = await allocationRunStore.FindByIdAsync(allocationRunId, cancellationToken);
        if (allocationRun is null)
        {
            return new(CreateOfferOutcome.AllocationRunNotFound);
        }

        if (allocationRun.RecommendedDriverId is null)
        {
            return new(CreateOfferOutcome.NoRecommendedDriver);
        }

        if (await offerStore.FindByAllocationRunIdAsync(allocationRunId, cancellationToken) is not null)
        {
            return new(CreateOfferOutcome.OfferAlreadyExists);
        }

        var offer = Offer.Create(allocationRunId, allocationRun.RecommendedDriverId.Value, policy.Ttl, timeProvider.GetUtcNow());
        await offerStore.AddAsync(offer, cancellationToken);
        try
        {
            await offerStore.SaveChangesAsync(cancellationToken);
        }
        catch (OfferAlreadyExistsException)
        {
            return new(CreateOfferOutcome.OfferAlreadyExists);
        }

        return new(CreateOfferOutcome.Success, OfferView.FromOffer(offer));
    }
}
