namespace RideDispatch.Domain.Dispatch;

public sealed class Offer
{
    private Offer()
    {
    }

    private Offer(Guid id, Guid allocationRunId, Guid driverId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        AllocationRunId = allocationRunId;
        DriverId = driverId;
        Status = OfferStatus.Pending;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid AllocationRunId { get; private set; }
    public Guid DriverId { get; private set; }
    public OfferStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static Offer Create(Guid allocationRunId, Guid driverId, TimeSpan ttl, DateTimeOffset currentUtcTime)
    {
        if (allocationRunId == Guid.Empty)
        {
            throw new ArgumentException("Allocation run ID must not be empty.", nameof(allocationRunId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver ID must not be empty.", nameof(driverId));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Offer TTL must be greater than zero.");
        }

        var createdAt = currentUtcTime.ToUniversalTime();
        return new Offer(Guid.CreateVersion7(), allocationRunId, driverId, createdAt, createdAt.Add(ttl));
    }

    public void Accept(DateTimeOffset currentUtcTime) => Resolve(OfferStatus.Accepted, currentUtcTime, false);
    public void Decline(DateTimeOffset currentUtcTime) => Resolve(OfferStatus.Declined, currentUtcTime, false);
    public void Withdraw(DateTimeOffset currentUtcTime) => Resolve(OfferStatus.Withdrawn, currentUtcTime, false);
    public void Expire(DateTimeOffset currentUtcTime) => Resolve(OfferStatus.Expired, currentUtcTime, true);

    private void Resolve(OfferStatus status, DateTimeOffset currentUtcTime, bool requiresExpiry)
    {
        if (Status != OfferStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending offer can transition.");
        }

        var resolvedAt = currentUtcTime.ToUniversalTime();

        if (resolvedAt < CreatedAt)
        {
            throw new InvalidOperationException(
                "An offer cannot resolve before it was created.");
        }

        var hasExpired = resolvedAt >= ExpiresAt;
        if (requiresExpiry != hasExpired)
        {
            throw new InvalidOperationException(requiresExpiry
                ? "An offer cannot expire before its expiry time."
                : "An expired offer cannot be resolved by the requested action.");
        }

        Status = status;
        ResolvedAt = resolvedAt;
    }
}
