using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public sealed record AllocationCandidateEvaluationView(
    Guid DriverId,
    bool IsEligible,
    int? Rank,
    double DistanceMeters,
    DateTimeOffset? AvailableSince,
    DateTimeOffset LocationRecordedAt,
    IReadOnlyList<EligibilityRejectionReason> RejectionReasons)
{
    public static AllocationCandidateEvaluationView FromEvaluation(
        AllocationCandidateEvaluation evaluation) =>
        new(
            evaluation.DriverId,
            evaluation.IsEligible,
            evaluation.Rank,
            evaluation.DistanceMeters,
            evaluation.AvailableSince,
            evaluation.LocationRecordedAt,
            evaluation.Rejections
                .OrderBy(rejection => rejection.Reason)
                .Select(rejection => rejection.Reason)
                .ToList());
}
