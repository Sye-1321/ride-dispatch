using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.Dispatch.Ranking;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Ranking;

public sealed class DriverRankingTests
{
    private static readonly DateTimeOffset AvailableSince =
        new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nearest_policy_uses_distance_first()
    {
        var nearerNewer = Driver("00000000-0000-0000-0000-000000000001", 100, AvailableSince.AddHours(1));
        var fartherOlder = Driver("00000000-0000-0000-0000-000000000002", 200, AvailableSince);

        var result = DriverRanking.Rank([fartherOlder, nearerNewer], DispatchRankingPolicy.Nearest);

        Assert.Equal([nearerNewer, fartherOlder], result);
    }

    [Fact]
    public void Longest_idle_policy_uses_availability_time_first()
    {
        var nearerNewer = Driver("00000000-0000-0000-0000-000000000001", 100, AvailableSince.AddHours(1));
        var fartherOlder = Driver("00000000-0000-0000-0000-000000000002", 200, AvailableSince);

        var result = DriverRanking.Rank(
            [nearerNewer, fartherOlder],
            DispatchRankingPolicy.LongestIdle);

        Assert.Equal([fartherOlder, nearerNewer], result);
    }

    [Fact]
    public void Undefined_policy_fails_explicitly()
    {
        var undefinedPolicy = (DispatchRankingPolicy)int.MaxValue;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => DriverRanking.Rank([], undefinedPolicy));

        Assert.Equal("policy", exception.ParamName);
        Assert.Equal(undefinedPolicy, exception.ActualValue);
    }

    private static EligibleDriverView Driver(
        string driverId,
        double distanceMeters,
        DateTimeOffset availableSince) =>
        new(Guid.Parse(driverId), distanceMeters, availableSince, AvailableSince);
}
