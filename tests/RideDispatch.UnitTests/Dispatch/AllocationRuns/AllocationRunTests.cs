using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.AllocationRuns;

public sealed class AllocationRunTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 8, 15, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Creation_captures_all_run_state_with_uuid_version_7()
    {
        var rideRequestId = Guid.CreateVersion7();
        var driverId = Guid.CreateVersion7();

        var run = AllocationRun.Create(
            rideRequestId,
            DispatchRankingPolicy.LongestIdle,
            driverId,
            CurrentTime);

        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal(7, run.Id.Version);
        Assert.Equal(rideRequestId, run.RideRequestId);
        Assert.Equal(DispatchRankingPolicy.LongestIdle, run.RankingPolicy);
        Assert.Equal(driverId, run.RecommendedDriverId);
        Assert.Equal(CurrentTime.ToUniversalTime(), run.CreatedAt);
    }

    [Fact]
    public void Recommendation_may_be_absent()
    {
        var run = AllocationRun.Create(
            Guid.CreateVersion7(),
            DispatchRankingPolicy.Nearest,
            null,
            CurrentTime);

        Assert.Null(run.RecommendedDriverId);
    }

    [Fact]
    public void Structurally_invalid_values_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => AllocationRun.Create(
            Guid.Empty,
            DispatchRankingPolicy.Nearest,
            null,
            CurrentTime));
        Assert.Throws<ArgumentOutOfRangeException>(() => AllocationRun.Create(
            Guid.CreateVersion7(),
            (DispatchRankingPolicy)int.MaxValue,
            null,
            CurrentTime));
        Assert.Throws<ArgumentException>(() => AllocationRun.Create(
            Guid.CreateVersion7(),
            DispatchRankingPolicy.Nearest,
            Guid.Empty,
            CurrentTime));
    }
}
