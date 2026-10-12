namespace RideDispatch.Domain.Dispatch;

public sealed class Assignment
{
    private Assignment() { }

    private Assignment(Guid id, Guid offerId, Guid rideRequestId, Guid driverId,
        DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset createdAt)
    {
        Id = id;
        OfferId = offerId;
        RideRequestId = rideRequestId;
        DriverId = driverId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid RideRequestId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Assignment Create(Guid offerId, Guid rideRequestId, Guid driverId,
        DateTimeOffset startsAt, TimeSpan duration, DateTimeOffset currentUtcTime)
    {
        if (offerId == Guid.Empty) throw new ArgumentException("Offer ID must not be empty.", nameof(offerId));
        if (rideRequestId == Guid.Empty) throw new ArgumentException("Ride request ID must not be empty.", nameof(rideRequestId));
        if (driverId == Guid.Empty) throw new ArgumentException("Driver ID must not be empty.", nameof(driverId));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");

        var normalizedStart = startsAt.ToUniversalTime();
        var createdAt = currentUtcTime.ToUniversalTime();
        if (normalizedStart < createdAt) throw new ArgumentException("Assignment cannot start before it is created.", nameof(startsAt));

        return new Assignment(Guid.CreateVersion7(), offerId, rideRequestId, driverId,
            normalizedStart, normalizedStart.Add(duration), createdAt);
    }
}
