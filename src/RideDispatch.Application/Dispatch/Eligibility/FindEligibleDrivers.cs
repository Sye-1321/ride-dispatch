using RideDispatch.Application.RideRequests;

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
    IRideRequestStore rideRequestStore,
    IDispatchCandidateStore candidateStore,
    DispatchEligibilityPolicy policy,
    TimeProvider timeProvider)
{
    public async Task<FindEligibleDriversResult> ExecuteAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var rideRequest = await rideRequestStore.FindByIdAsync(rideRequestId, cancellationToken);
        if (rideRequest is null)
        {
            return new(FindEligibleDriversOutcome.RideRequestNotFound);
        }

        var candidates = await candidateStore.FindWithinRadiusAsync(
            rideRequest.PickupLatitude,
            rideRequest.PickupLongitude,
            policy.MaxRadiusMeters,
            cancellationToken);
        var currentUtcTime = timeProvider.GetUtcNow();

        var eligibleDrivers = candidates
            .Select(candidate => CandidateEligibilityEvaluator.Evaluate(
                candidate,
                rideRequest.RequiredVehicleType,
                currentUtcTime,
                policy))
            .Where(evaluation => evaluation.IsEligible)
            .Select(evaluation => new EligibleDriverView(
                evaluation.Candidate.DriverId,
                evaluation.Candidate.DistanceMeters,
                evaluation.Candidate.AvailableSince!.Value,
                evaluation.Candidate.LocationRecordedAt))
            .OrderBy(driver => driver.DriverId)
            .ToList();

        return new(FindEligibleDriversOutcome.Success, eligibleDrivers);
    }
}
