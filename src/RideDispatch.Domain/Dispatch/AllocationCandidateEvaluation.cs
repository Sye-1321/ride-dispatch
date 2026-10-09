namespace RideDispatch.Domain.Dispatch;

public sealed class AllocationCandidateEvaluation
{
    private readonly List<AllocationCandidateRejection> rejections = [];

    private AllocationCandidateEvaluation()
    {
    }

    private AllocationCandidateEvaluation(
        Guid allocationRunId,
        Guid driverId,
        bool isEligible,
        int? rank,
        double distanceMeters,
        DateTimeOffset? availableSince,
        DateTimeOffset locationRecordedAt)
    {
        AllocationRunId = allocationRunId;
        DriverId = driverId;
        IsEligible = isEligible;
        Rank = rank;
        DistanceMeters = distanceMeters;
        AvailableSince = availableSince;
        LocationRecordedAt = locationRecordedAt;
    }

    public Guid AllocationRunId { get; private set; }

    public Guid DriverId { get; private set; }

    public bool IsEligible { get; private set; }

    public int? Rank { get; private set; }

    public double DistanceMeters { get; private set; }

    public DateTimeOffset? AvailableSince { get; private set; }

    public DateTimeOffset LocationRecordedAt { get; private set; }

    public IReadOnlyCollection<AllocationCandidateRejection> Rejections => rejections;

    public static AllocationCandidateEvaluation Create(
        Guid allocationRunId,
        Guid driverId,
        bool isEligible,
        int? rank,
        double distanceMeters,
        DateTimeOffset? availableSince,
        DateTimeOffset locationRecordedAt,
        IReadOnlyCollection<EligibilityRejectionReason> rejectionReasons)
    {
        ArgumentNullException.ThrowIfNull(rejectionReasons);

        if (allocationRunId == Guid.Empty)
        {
            throw new ArgumentException("Allocation run ID must not be empty.", nameof(allocationRunId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver ID must not be empty.", nameof(driverId));
        }

        if (!double.IsFinite(distanceMeters) || distanceMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        }

        if (isEligible && (rank is null || rank <= 0))
        {
            throw new ArgumentException(
                "Eligible evaluations require a positive rank.",
                nameof(rank));
        }

        if (!isEligible && rank is not null)
        {
            throw new ArgumentException(
                "Rejected evaluations must not have a rank.",
                nameof(rank));
        }

        if (isEligible && availableSince is null)
        {
            throw new ArgumentException(
                "Eligible evaluations require an availability timestamp.",
                nameof(availableSince));
        }

        if (isEligible && rejectionReasons.Count > 0)
        {
            throw new ArgumentException("Eligible evaluations must not have rejection reasons.", nameof(rejectionReasons));
        }

        if (!isEligible && rejectionReasons.Count == 0)
        {
            throw new ArgumentException("Rejected evaluations require a rejection reason.", nameof(rejectionReasons));
        }

        var evaluation = new AllocationCandidateEvaluation(
            allocationRunId,
            driverId,
            isEligible,
            rank,
            distanceMeters,
            availableSince?.ToUniversalTime(),
            locationRecordedAt.ToUniversalTime());

        foreach (var reason in rejectionReasons.Distinct())
        {
            evaluation.rejections.Add(AllocationCandidateRejection.Create(
                allocationRunId,
                driverId,
                reason));
        }

        return evaluation;
    }
}
