namespace RideDispatch.Application.Dispatch.Eligibility;

public enum EligibilityRejectionReason
{
    DriverNotApproved,
    DriverNotAvailable,
    AvailabilityTimestampMissing,
    VehicleMissing,
    VehicleTypeMismatch,
    LocationStale,
    FinancialStandingMissing,
    CommissionBalanceBelowMinimum,
}
