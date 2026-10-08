namespace RideDispatch.Application.Dispatch.AllocationRuns;

public sealed class GetAllocationRun(IAllocationRunStore allocationRunStore)
{
    public async Task<AllocationRunView?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var allocationRun = await allocationRunStore.FindByIdAsync(id, cancellationToken);
        return allocationRun is null ? null : AllocationRunView.FromAllocationRun(allocationRun);
    }
}
