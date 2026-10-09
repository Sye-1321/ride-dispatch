using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Eligibility;

public enum FindEligibleDriversOutcome
{
    Success,
    RideRequestNotFound,
}

public sealed record FindEligibleDriversResult(
    FindEligibleDriversOutcome Outcome,
    IReadOnlyList<EligibleDriverView>? Drivers = null);

public sealed class FindEligibleDrivers(
    EvaluateDispatchCandidates evaluateDispatchCandidates)
{
    public async Task<FindEligibleDriversResult> ExecuteAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        CancellationToken cancellationToken)
    {
        var evaluation = await evaluateDispatchCandidates.ExecuteAsync(
            rideRequestId,
            rankingPolicy,
            cancellationToken);
        if (evaluation.Outcome == EvaluateDispatchCandidatesOutcome.RideRequestNotFound)
        {
            return new(FindEligibleDriversOutcome.RideRequestNotFound);
        }

        return new(FindEligibleDriversOutcome.Success, evaluation.RankedEligibleDrivers);
    }
}
