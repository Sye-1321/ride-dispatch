using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Application.Dispatch.Offers;

public enum CreateDispatcherOverrideOfferOutcome
{
    Success,
    AllocationRunNotFound,
    DriverNotEvaluated,
    DriverNotEligible,
    DriverAlreadyRecommended,
    InvalidReason,
    OfferAlreadyExists,
    ScheduledPickupElapsed,
}

public sealed record CreateDispatcherOverrideOfferResult(
    CreateDispatcherOverrideOfferOutcome Outcome, OfferView? Offer = null);

public sealed class CreateDispatcherOverrideOffer(
    IAllocationRunStore allocationRunStore,
    IRideRequestStore rideRequestStore,
    IOfferStore offerStore,
    OfferPolicy policy,
    TimeProvider timeProvider)
{
    public async Task<CreateDispatcherOverrideOfferResult> ExecuteAsync(
        Guid allocationRunId,
        Guid driverId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var allocationRun = await allocationRunStore.FindByIdAsync(allocationRunId, cancellationToken);
        if (allocationRun is null)
        {
            return new(CreateDispatcherOverrideOfferOutcome.AllocationRunNotFound);
        }

        var evaluations = await allocationRunStore.FindCandidateEvaluationsAsync(
            allocationRunId, cancellationToken)
            ?? throw new InvalidOperationException("The allocation run candidate evidence is missing.");
        var evaluation = evaluations.SingleOrDefault(candidate => candidate.DriverId == driverId);
        if (evaluation is null)
        {
            return new(CreateDispatcherOverrideOfferOutcome.DriverNotEvaluated);
        }

        if (!evaluation.IsEligible)
        {
            return new(CreateDispatcherOverrideOfferOutcome.DriverNotEligible);
        }

        if (evaluation.Rank is null or <= 0)
        {
            throw new InvalidOperationException("An eligible candidate has no positive persisted rank.");
        }

        if (allocationRun.RecommendedDriverId is null)
        {
            throw new InvalidOperationException("An eligible candidate exists without a recommended driver.");
        }

        if (allocationRun.RecommendedDriverId == driverId)
        {
            return new(CreateDispatcherOverrideOfferOutcome.DriverAlreadyRecommended);
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrEmpty(normalizedReason) || normalizedReason.Length > 500)
        {
            return new(CreateDispatcherOverrideOfferOutcome.InvalidReason);
        }

        if (await offerStore.FindByAllocationRunIdAsync(allocationRunId, cancellationToken) is not null)
        {
            return new(CreateDispatcherOverrideOfferOutcome.OfferAlreadyExists);
        }

        var rideRequest = await rideRequestStore.FindByIdAsync(allocationRun.RideRequestId, cancellationToken)
            ?? throw new InvalidOperationException("An allocation run references a missing ride request.");

        var now = timeProvider.GetUtcNow();

        if (rideRequest.Timing == RideTiming.Scheduled &&
            rideRequest.HasScheduledPickupElapsed(now))
        {
            return new(CreateDispatcherOverrideOfferOutcome.ScheduledPickupElapsed);
        }

        if (!await new RideOfferAvailability(offerStore).CanCreateAsync(
                allocationRun.RideRequestId, now, cancellationToken))
        {
            return new(CreateDispatcherOverrideOfferOutcome.OfferAlreadyExists);
        }

        var offer = Offer.CreateDispatcherOverride(
            allocationRunId, allocationRun.RideRequestId, driverId, normalizedReason, policy.Ttl, now);
        await offerStore.AddAsync(offer, cancellationToken);
        try
        {
            await offerStore.SaveChangesAsync(cancellationToken);
        }
        catch (OfferAlreadyExistsException)
        {
            return new(CreateDispatcherOverrideOfferOutcome.OfferAlreadyExists);
        }

        return new(CreateDispatcherOverrideOfferOutcome.Success, OfferView.FromOffer(offer));
    }
}
