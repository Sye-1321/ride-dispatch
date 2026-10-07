using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.Dispatch.Eligibility;

public sealed record DispatchCandidateSnapshot(
    Guid DriverId,
    DriverApprovalStatus ApprovalStatus,
    DriverOperationalStatus OperationalStatus,
    DateTimeOffset? AvailableSince,
    VehicleType? VehicleType,
    DateTimeOffset LocationRecordedAt,
    double DistanceMeters,
    decimal? CommissionBalance);
