using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.Dispatch.Eligibility;

public static class CandidateEligibilityEvaluator
{
    public static CandidateEligibilityEvaluation Evaluate(
        DispatchCandidateSnapshot candidate,
        VehicleType requiredVehicleType,
        DateTimeOffset currentUtcTime,
        DispatchEligibilityPolicy policy)
    {
        var rejectionReasons = new List<EligibilityRejectionReason>();

        if (candidate.ApprovalStatus != DriverApprovalStatus.Approved)
        {
            rejectionReasons.Add(EligibilityRejectionReason.DriverNotApproved);
        }

        if (candidate.OperationalStatus != DriverOperationalStatus.Available)
        {
            rejectionReasons.Add(EligibilityRejectionReason.DriverNotAvailable);
        }

        if (candidate.AvailableSince is null)
        {
            rejectionReasons.Add(EligibilityRejectionReason.AvailabilityTimestampMissing);
        }

        if (candidate.VehicleType is null)
        {
            rejectionReasons.Add(EligibilityRejectionReason.VehicleMissing);
        }
        else if (candidate.VehicleType != requiredVehicleType)
        {
            rejectionReasons.Add(EligibilityRejectionReason.VehicleTypeMismatch);
        }

        if (candidate.LocationRecordedAt < currentUtcTime - policy.LocationFreshness)
        {
            rejectionReasons.Add(EligibilityRejectionReason.LocationStale);
        }

        if (candidate.CommissionBalance is null)
        {
            rejectionReasons.Add(EligibilityRejectionReason.FinancialStandingMissing);
        }
        else if (candidate.CommissionBalance < policy.MinimumCommissionBalance)
        {
            rejectionReasons.Add(EligibilityRejectionReason.CommissionBalanceBelowMinimum);
        }

        return new(candidate, rejectionReasons);
    }
}
