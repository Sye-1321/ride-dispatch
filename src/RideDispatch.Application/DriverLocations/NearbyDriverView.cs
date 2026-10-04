namespace RideDispatch.Application.DriverLocations;

public sealed record NearbyDriverView(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    double DistanceMeters);
