using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public interface IAllocationRunStore
{
    Task AddAsync(AllocationRun allocationRun, CancellationToken cancellationToken);

    Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AllocationRun?> FindBySourceOfferIdAsync(Guid sourceOfferId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(
        Guid allocationRunId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ReplacementAllocationRunAlreadyExistsException : Exception
{
    public ReplacementAllocationRunAlreadyExistsException(Exception innerException)
        : base("A replacement allocation run already exists for the source offer.", innerException)
    {
    }
}
