namespace RideDispatch.Application.Dispatch.Eligibility;

public interface IDispatchCandidateStore
{
    Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
        double pickupLatitude,
        double pickupLongitude,
        double radiusMeters,
        CancellationToken cancellationToken);
}
