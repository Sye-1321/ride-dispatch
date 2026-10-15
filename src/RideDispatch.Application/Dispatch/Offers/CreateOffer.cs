using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Application.Dispatch.Offers;

public enum CreateOfferOutcome
{
    Success,
    AllocationRunNotFound,
    NoRecommendedDriver,
    OfferAlreadyExists,
    ScheduledPickupElapsed,
}

public sealed record CreateOfferResult(CreateOfferOutcome Outcome, OfferView? Offer = null);

public sealed class CreateOffer(
    IAllocationRunStore allocationRunStore,
    IRideRequestStore rideRequestStore,
    IOfferStore offerStore,
    OfferPolicy policy,
    TimeProvider timeProvider)
{
    public Task<CreateOfferResult> ExecuteAsync(
        Guid allocationRunId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(allocationRunId, null, cancellationToken);

    public async Task<CreateOfferResult> ExecuteAsync(
        Guid allocationRunId,
        DateTimeOffset? capturedAt,
        CancellationToken cancellationToken)
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

        var rideRequest = await rideRequestStore.FindByIdAsync(allocationRun.RideRequestId, cancellationToken)
            ?? throw new InvalidOperationException("An allocation run references a missing ride request.");

        var now = capturedAt ?? timeProvider.GetUtcNow();

        if (rideRequest.Timing == RideTiming.Scheduled &&
            rideRequest.HasScheduledPickupElapsed(now))
        {
            return new(CreateOfferOutcome.ScheduledPickupElapsed);
        }

        var offer = Offer.Create(
            allocationRunId,
            allocationRun.RecommendedDriverId.Value,
            policy.Ttl,
            now);
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
