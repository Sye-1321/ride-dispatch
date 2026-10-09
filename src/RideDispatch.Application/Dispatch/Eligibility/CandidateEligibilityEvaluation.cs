using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Eligibility;

public sealed record CandidateEligibilityEvaluation(
    DispatchCandidateSnapshot Candidate,
    IReadOnlyList<EligibilityRejectionReason> RejectionReasons)
{
    public bool IsEligible => RejectionReasons.Count == 0;
}
