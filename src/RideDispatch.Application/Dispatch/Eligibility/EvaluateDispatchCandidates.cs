using RideDispatch.Application.Dispatch.Ranking;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Eligibility;

public enum EvaluateDispatchCandidatesOutcome
{
    Success,
    RideRequestNotFound,
}

public sealed record EvaluateDispatchCandidatesResult(
    EvaluateDispatchCandidatesOutcome Outcome,
    IReadOnlyList<CandidateEligibilityEvaluation>? CandidateEvaluations = null,
    IReadOnlyList<EligibleDriverView>? RankedEligibleDrivers = null);

public sealed class EvaluateDispatchCandidates(
    IRideRequestStore rideRequestStore,
    IDispatchCandidateStore candidateStore,
    DispatchEligibilityPolicy policy,
    TimeProvider timeProvider)
{
    public async Task<EvaluateDispatchCandidatesResult> ExecuteAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        CancellationToken cancellationToken)
    {
        var rideRequest = await rideRequestStore.FindByIdAsync(rideRequestId, cancellationToken);
        if (rideRequest is null)
        {
            return new(EvaluateDispatchCandidatesOutcome.RideRequestNotFound);
        }

        var candidates = await candidateStore.FindWithinRadiusAsync(
            rideRequest.PickupLatitude,
            rideRequest.PickupLongitude,
            policy.MaxRadiusMeters,
            cancellationToken);
        var currentUtcTime = timeProvider.GetUtcNow();
        var evaluations = candidates
            .Select(candidate => CandidateEligibilityEvaluator.Evaluate(
                candidate,
                rideRequest.RequiredVehicleType,
                currentUtcTime,
                policy))
            .ToList();
        var eligibleDrivers = evaluations
            .Where(evaluation => evaluation.IsEligible)
            .Select(evaluation => new EligibleDriverView(
                evaluation.Candidate.DriverId,
                evaluation.Candidate.DistanceMeters,
                evaluation.Candidate.AvailableSince!.Value,
                evaluation.Candidate.LocationRecordedAt));

        return new(
            EvaluateDispatchCandidatesOutcome.Success,
            evaluations,
            DriverRanking.Rank(eligibleDrivers, rankingPolicy));
    }
}
