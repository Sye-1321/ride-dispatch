namespace RideDispatch.Domain.Dispatch;

public sealed class AllocationRun
{
    private readonly List<AllocationCandidateEvaluation> candidateEvaluations = [];

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

    public IReadOnlyCollection<AllocationCandidateEvaluation> CandidateEvaluations =>
        candidateEvaluations;

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

    public void AddCandidateEvaluation(AllocationCandidateEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (evaluation.AllocationRunId != Id)
        {
            throw new ArgumentException(
                "Candidate evaluation belongs to another allocation run.",
                nameof(evaluation));
        }

        if (candidateEvaluations.Any(existing => existing.DriverId == evaluation.DriverId))
        {
            throw new ArgumentException(
                "Driver already has an evaluation for this allocation run.",
                nameof(evaluation));
        }

        if (evaluation.IsEligible)
        {
            if (RecommendedDriverId is null)
            {
                throw new ArgumentException(
                    "An eligible candidate requires a recommended driver.",
                    nameof(evaluation));
            }

            if (evaluation.Rank == 1 && evaluation.DriverId != RecommendedDriverId)
            {
                throw new ArgumentException(
                    "The first-ranked candidate must be the recommended driver.",
                    nameof(evaluation));
            }

            if (evaluation.DriverId == RecommendedDriverId && evaluation.Rank != 1)
            {
                throw new ArgumentException(
                    "The recommended driver must be the first-ranked candidate.",
                    nameof(evaluation));
            }

            if (candidateEvaluations.Any(existing =>
                    existing.IsEligible &&
                    existing.Rank == evaluation.Rank))
            {
                throw new ArgumentException(
                    "Eligible candidates must have unique ranks within an allocation run.",
                    nameof(evaluation));
            }
        }
        else if (evaluation.DriverId == RecommendedDriverId)
        {
            throw new ArgumentException(
                "The recommended driver cannot have a rejected evaluation.",
                nameof(evaluation));
        }

        candidateEvaluations.Add(evaluation);
    }
}
