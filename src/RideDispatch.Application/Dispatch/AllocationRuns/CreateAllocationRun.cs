using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public enum CreateAllocationRunOutcome
{
    Success,
    RideRequestNotFound,
}

public sealed record CreateAllocationRunResult(
    CreateAllocationRunOutcome Outcome,
    AllocationRunView? AllocationRun = null);

public sealed class CreateAllocationRun(
    EvaluateDispatchCandidates evaluateDispatchCandidates,
    IAllocationRunStore allocationRunStore,
    TimeProvider timeProvider)
{
    public async Task<CreateAllocationRunResult> ExecuteAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        CancellationToken cancellationToken)
    {
        var dispatchEvaluation = await evaluateDispatchCandidates.ExecuteAsync(
            rideRequestId,
            rankingPolicy,
            cancellationToken);

        if (dispatchEvaluation.Outcome == EvaluateDispatchCandidatesOutcome.RideRequestNotFound)
        {
            return new(CreateAllocationRunOutcome.RideRequestNotFound);
        }

        var rankedEligibleDrivers = dispatchEvaluation.RankedEligibleDrivers!;
        var allocationRun = AllocationRun.Create(
            rideRequestId,
            rankingPolicy,
            rankedEligibleDrivers.FirstOrDefault()?.DriverId,
            timeProvider.GetUtcNow());

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

        return new(
            CreateAllocationRunOutcome.Success,
            AllocationRunView.FromAllocationRun(allocationRun));
    }
}
