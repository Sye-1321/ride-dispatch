namespace RideDispatch.Application.Dispatch.Eligibility;

public sealed record EligibleDriverView(
    Guid DriverId,
    double DistanceMeters,
    DateTimeOffset AvailableSince,
    DateTimeOffset LocationRecordedAt);
