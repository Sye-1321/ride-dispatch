using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public enum CreateAllocationRunOutcome
{
    Success,
    RideRequestNotFound,
}

public sealed record CreateAllocationRunResult(
    CreateAllocationRunOutcome Outcome,
    AllocationRunView? AllocationRun = null);

public sealed class CreateAllocationRun(
    AllocationRunCreator allocationRunCreator)
{
    public async Task<CreateAllocationRunResult> ExecuteAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        CancellationToken cancellationToken)
    {
        var allocationRun = await allocationRunCreator.CreateAsync(
            rideRequestId,
            rankingPolicy,
            null,
            cancellationToken);

        if (allocationRun is null)
        {
            return new(CreateAllocationRunOutcome.RideRequestNotFound);
        }

        return new(
            CreateAllocationRunOutcome.Success,
            AllocationRunView.FromAllocationRun(allocationRun));
    }
}
