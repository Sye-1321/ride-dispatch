using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Application.Dispatch.Offers;

public enum CreateReplacementDispatchOutcome
{
    Success,
    OfferNotFound,
    SourceOfferNotFailed,
    ScheduledPickupElapsed,
}

public sealed record CreateReplacementDispatchResult(
    CreateReplacementDispatchOutcome Outcome,
    Guid SourceOfferId,
    AllocationRunView? AllocationRun = null,
    OfferView? Offer = null);

public sealed class CreateReplacementDispatch(
    IOfferStore offerStore,
    IAllocationRunStore allocationRunStore,
    IRideRequestStore rideRequestStore,
    AllocationRunCreator allocationRunCreator,
    CreateOffer createOffer,
    TimeProvider timeProvider)
{
    public async Task<CreateReplacementDispatchResult> ExecuteAsync(
        Guid sourceOfferId,
        CancellationToken cancellationToken)
    {
        var sourceOffer = await offerStore.FindByIdAsync(sourceOfferId, cancellationToken);
        if (sourceOffer is null)
        {
            return new(CreateReplacementDispatchOutcome.OfferNotFound, sourceOfferId);
        }

        DateTimeOffset? decisionTime = null;
        if (sourceOffer.Status == OfferStatus.Pending)
        {
            var now = timeProvider.GetUtcNow();
            if (now < sourceOffer.ExpiresAt)
            {
                return new(CreateReplacementDispatchOutcome.SourceOfferNotFailed, sourceOfferId);
            }

            sourceOffer.Expire(now);
            try
            {
                await offerStore.SaveChangesAsync(cancellationToken);
            }
            catch (OfferStateConcurrencyException)
            {
                var reloaded = await offerStore.FindByIdReadOnlyAsync(sourceOfferId, cancellationToken);
                if (reloaded is null)
                {
                    return new(CreateReplacementDispatchOutcome.OfferNotFound, sourceOfferId);
                }

                sourceOffer = reloaded;
                if (sourceOffer.Status is not (OfferStatus.Declined or OfferStatus.Expired))
                {
                    return new(CreateReplacementDispatchOutcome.SourceOfferNotFailed, sourceOfferId);
                }
            }

            decisionTime = now;
        }
        else if (sourceOffer.Status is not (OfferStatus.Declined or OfferStatus.Expired))
        {
            return new(CreateReplacementDispatchOutcome.SourceOfferNotFailed, sourceOfferId);
        }

        var sourceRun = await allocationRunStore.FindByIdAsync(
            sourceOffer.AllocationRunId,
            cancellationToken)
            ?? throw new InvalidOperationException("The source offer references a missing allocation run.");

        var replacementRun = await allocationRunStore.FindBySourceOfferIdAsync(
            sourceOfferId,
            cancellationToken);
        if (replacementRun is null)
        {
            var rideRequest = await rideRequestStore.FindByIdAsync(
                sourceRun.RideRequestId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "The source allocation run references a missing ride request.");

            if (rideRequest.Timing == RideTiming.Scheduled)
            {
                decisionTime ??= timeProvider.GetUtcNow();
                if (rideRequest.HasScheduledPickupElapsed(decisionTime.Value))
                {
                    return new(CreateReplacementDispatchOutcome.ScheduledPickupElapsed, sourceOfferId);
                }
            }

            try
            {
                replacementRun = await allocationRunCreator.CreateAsync(
                    sourceRun.RideRequestId,
                    sourceRun.RankingPolicy,
                    sourceOfferId,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The source allocation run references a missing ride request.");
            }
            catch (ReplacementAllocationRunAlreadyExistsException)
            {
                replacementRun = await allocationRunStore.FindBySourceOfferIdAsync(
                    sourceOfferId,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The winning replacement allocation run could not be loaded.");
            }
        }

        if (replacementRun.RecommendedDriverId is null)
        {
            return Success(sourceOfferId, replacementRun, null);
        }

        var replacementOffer = await offerStore.FindByAllocationRunIdAsync(
            replacementRun.Id,
            cancellationToken);
        if (replacementOffer is null)
        {
            var createResult = await createOffer.ExecuteAsync(
                replacementRun.Id,
                decisionTime,
                cancellationToken);
            if (createResult.Outcome == CreateOfferOutcome.Success)
            {
                return new(
                    CreateReplacementDispatchOutcome.Success,
                    sourceOfferId,
                    AllocationRunView.FromAllocationRun(replacementRun),
                    createResult.Offer);
            }

            if (createResult.Outcome == CreateOfferOutcome.ScheduledPickupElapsed)
            {
                return new(CreateReplacementDispatchOutcome.ScheduledPickupElapsed, sourceOfferId);
            }

            if (createResult.Outcome != CreateOfferOutcome.OfferAlreadyExists)
            {
                throw new InvalidOperationException(
                    $"Offer creation failed unexpectedly with outcome {createResult.Outcome}.");
            }

            replacementOffer = await offerStore.FindByAllocationRunIdAsync(
                replacementRun.Id,
                cancellationToken)
                ?? throw new InvalidOperationException("The winning replacement offer could not be loaded.");
        }

        return Success(sourceOfferId, replacementRun, replacementOffer);
    }

    private static CreateReplacementDispatchResult Success(
        Guid sourceOfferId,
        AllocationRun allocationRun,
        Offer? offer) =>
        new(
            CreateReplacementDispatchOutcome.Success,
            sourceOfferId,
            AllocationRunView.FromAllocationRun(allocationRun),
            offer is null ? null : OfferView.FromOffer(offer));
}
