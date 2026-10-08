namespace RideDispatch.Domain.Dispatch;

public sealed class AllocationRun
{
    private AllocationRun()
    {
    }

    private AllocationRun(
        Guid id,
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        Guid? recommendedDriverId,
        DateTimeOffset createdAt)
    {
        Id = id;
        RideRequestId = rideRequestId;
        RankingPolicy = rankingPolicy;
        RecommendedDriverId = recommendedDriverId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RideRequestId { get; private set; }

    public DispatchRankingPolicy RankingPolicy { get; private set; }

    public Guid? RecommendedDriverId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AllocationRun Create(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        Guid? recommendedDriverId,
        DateTimeOffset currentUtcTime)
    {
        if (rideRequestId == Guid.Empty)
        {
            throw new ArgumentException("Ride request ID must not be empty.", nameof(rideRequestId));
        }

        if (!Enum.IsDefined(rankingPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(rankingPolicy));
        }

        if (recommendedDriverId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recommended driver ID must not be empty when supplied.",
                nameof(recommendedDriverId));
        }

        return new AllocationRun(
            Guid.CreateVersion7(),
            rideRequestId,
            rankingPolicy,
            recommendedDriverId,
            currentUtcTime.ToUniversalTime());
    }
}
