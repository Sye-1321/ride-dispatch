namespace RideDispatch.Application.Dispatch.AllocationRuns;

public sealed class GetAllocationCandidateEvaluations(IAllocationRunStore allocationRunStore)
{
    public async Task<IReadOnlyList<AllocationCandidateEvaluationView>?> ExecuteAsync(
        Guid allocationRunId,
        CancellationToken cancellationToken)
    {
        var evaluations = await allocationRunStore.FindCandidateEvaluationsAsync(
            allocationRunId,
            cancellationToken);
        return evaluations?.Select(AllocationCandidateEvaluationView.FromEvaluation).ToList();
    }
}
