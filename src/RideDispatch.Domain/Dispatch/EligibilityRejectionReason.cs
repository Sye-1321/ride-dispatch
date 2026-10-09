namespace RideDispatch.Domain.Dispatch;

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
