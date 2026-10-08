using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.AllocationRuns;

public sealed record AllocationRunView(
    Guid Id,
    Guid RideRequestId,
    DispatchRankingPolicy RankingPolicy,
    Guid? RecommendedDriverId,
    DateTimeOffset CreatedAt)
{
    public static AllocationRunView FromAllocationRun(AllocationRun allocationRun) =>
        new(
            allocationRun.Id,
            allocationRun.RideRequestId,
            allocationRun.RankingPolicy,
            allocationRun.RecommendedDriverId,
            allocationRun.CreatedAt);
}
