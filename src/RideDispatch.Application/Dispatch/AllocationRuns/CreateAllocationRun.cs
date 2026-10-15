using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public enum CreateAllocationRunOutcome
{
    Success,
    RideRequestNotFound,
    ScheduledDispatchNotActivated,
    ScheduledPickupElapsed,
}

public sealed record CreateAllocationRunResult(
    CreateAllocationRunOutcome Outcome,
    AllocationRunView? AllocationRun = null);

public sealed class CreateAllocationRun(
    AllocationRunCreator allocationRunCreator,
    IRideRequestStore rideRequestStore,
    TimeProvider timeProvider)
{
    public async Task<CreateAllocationRunResult> ExecuteAsync(
        Guid rideRequestId,
        DispatchRankingPolicy rankingPolicy,
        CancellationToken cancellationToken)
    {
        var rideRequest = await rideRequestStore.FindByIdAsync(rideRequestId, cancellationToken);
        if (rideRequest is null)
        {
            return new(CreateAllocationRunOutcome.RideRequestNotFound);
        }

        if (rideRequest.Timing == RideTiming.Scheduled)
        {
            if (rideRequest.DispatchActivatedAt is null)
            {
                return new(CreateAllocationRunOutcome.ScheduledDispatchNotActivated);
            }

            if (timeProvider.GetUtcNow() > rideRequest.RequestedPickupAt)
            {
                return new(CreateAllocationRunOutcome.ScheduledPickupElapsed);
            }
        }

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
