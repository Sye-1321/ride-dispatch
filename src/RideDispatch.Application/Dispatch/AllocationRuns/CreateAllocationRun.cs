using RideDispatch.Application.Dispatch.Eligibility;
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
    FindEligibleDrivers findEligibleDrivers,
    IAllocationRunStore allocationRunStore,
    TimeProvider timeProvider)
{
    public async Task<CreateAllocationRunResult> ExecuteAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        CancellationToken cancellationToken)
    {
        var eligibleDrivers = await findEligibleDrivers.ExecuteAsync(
            rideRequestId,
            rankingPolicy,
            cancellationToken);

        if (eligibleDrivers.Outcome == FindEligibleDriversOutcome.RideRequestNotFound)
        {
            return new(CreateAllocationRunOutcome.RideRequestNotFound);
        }

        var allocationRun = AllocationRun.Create(
            rideRequestId,
            rankingPolicy,
            eligibleDrivers.Drivers!.FirstOrDefault()?.DriverId,
            timeProvider.GetUtcNow());

        await allocationRunStore.AddAsync(allocationRun, cancellationToken);
        await allocationRunStore.SaveChangesAsync(cancellationToken);

        return new(
            CreateAllocationRunOutcome.Success,
            AllocationRunView.FromAllocationRun(allocationRun));
    }
}
