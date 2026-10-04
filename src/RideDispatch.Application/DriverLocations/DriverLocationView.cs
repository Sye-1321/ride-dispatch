namespace RideDispatch.Application.DriverLocations;

public sealed record DriverLocationView(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt);
