namespace RideDispatch.Domain.Dispatch;

public sealed class AllocationCandidateRejection
{
    private AllocationCandidateRejection()
    {
    }

    private AllocationCandidateRejection(
        Guid allocationRunId,
        Guid driverId,
        EligibilityRejectionReason reason)
    {
        AllocationRunId = allocationRunId;
        DriverId = driverId;
        Reason = reason;
    }

    public Guid AllocationRunId { get; private set; }

    public Guid DriverId { get; private set; }

    public EligibilityRejectionReason Reason { get; private set; }

    public static AllocationCandidateRejection Create(
        Guid allocationRunId,
        Guid driverId,
        EligibilityRejectionReason reason)
    {
        if (allocationRunId == Guid.Empty)
        {
            throw new ArgumentException("Allocation run ID must not be empty.", nameof(allocationRunId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver ID must not be empty.", nameof(driverId));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return new(allocationRunId, driverId, reason);
    }
}
