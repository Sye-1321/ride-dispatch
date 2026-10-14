using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public sealed class AllocationRunCreator(
    EvaluateDispatchCandidates evaluateDispatchCandidates,
    IAllocationRunStore allocationRunStore,
    TimeProvider timeProvider)
{
    public async Task<AllocationRun?> CreateAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        Guid? sourceOfferId,
        CancellationToken cancellationToken)
    {
        var dispatchEvaluation = await evaluateDispatchCandidates.ExecuteAsync(
            rideRequestId,
            rankingPolicy,
            cancellationToken);

        if (dispatchEvaluation.Outcome == EvaluateDispatchCandidatesOutcome.RideRequestNotFound)
        {
            return null;
        }

        var rankedEligibleDrivers = dispatchEvaluation.RankedEligibleDrivers!;
        var recommendedDriverId = rankedEligibleDrivers.FirstOrDefault()?.DriverId;
        var now = timeProvider.GetUtcNow();
        var allocationRun = sourceOfferId is null
            ? AllocationRun.Create(rideRequestId, rankingPolicy, recommendedDriverId, now)
            : AllocationRun.CreateReplacement(
                rideRequestId,
                rankingPolicy,
                recommendedDriverId,
                sourceOfferId.Value,
                now);

        var ranks = rankedEligibleDrivers
            .Select((driver, index) => new { driver.DriverId, Rank = index + 1 })
            .ToDictionary(item => item.DriverId, item => item.Rank);

        foreach (var evaluation in dispatchEvaluation.CandidateEvaluations!)
        {
            var candidate = evaluation.Candidate;
            allocationRun.AddCandidateEvaluation(AllocationCandidateEvaluation.Create(
                allocationRun.Id,
                candidate.DriverId,
                evaluation.IsEligible,
                evaluation.IsEligible ? ranks[candidate.DriverId] : null,
                candidate.DistanceMeters,
                candidate.AvailableSince,
                candidate.LocationRecordedAt,
                evaluation.RejectionReasons));
        }

        await allocationRunStore.AddAsync(allocationRun, cancellationToken);
        await allocationRunStore.SaveChangesAsync(cancellationToken);
        return allocationRun;
    }
}
