namespace RideDispatch.Domain.RideRequests;

public readonly record struct RideOccupancyWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public static RideOccupancyWindow From(RideRequest rideRequest, DateTimeOffset currentUtcTime)
    {
        ArgumentNullException.ThrowIfNull(rideRequest);
        var startsAt = rideRequest.Timing == RideTiming.Immediate
            ? currentUtcTime.ToUniversalTime()
            : rideRequest.RequestedPickupAt!.Value.ToUniversalTime();
        return new(startsAt, startsAt.AddSeconds(rideRequest.EstimatedTripDurationSeconds));
    }
}
