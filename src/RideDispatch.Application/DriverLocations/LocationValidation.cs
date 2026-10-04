namespace RideDispatch.Application.DriverLocations;

internal static class LocationValidation
{
    public static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Latitude must be finite and between -90 and 90 degrees.");
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                "Longitude must be finite and between -180 and 180 degrees.");
        }
    }

    public static void ValidateRadius(double radiusMeters)
    {
        if (!double.IsFinite(radiusMeters) || radiusMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMeters),
                "Radius must be finite and greater than zero.");
        }
    }
}
