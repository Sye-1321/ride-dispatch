namespace RideDispatch.Application.Dispatch.Eligibility;

public interface IDispatchCandidateStore
{
    Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
        Guid rideRequestId,
        double pickupLatitude,
        double pickupLongitude,
        double radiusMeters,
        DateTimeOffset requestedStartsAt,
        DateTimeOffset requestedEndsAt,
        CancellationToken cancellationToken);
}
