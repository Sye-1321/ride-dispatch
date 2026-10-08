using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.Dispatch.Ranking;

namespace RideDispatch.UnitTests.Dispatch.Ranking;

public sealed class LongestIdleDriverRankingTests
{
    private static readonly DateTimeOffset AvailableSince =
        new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Availability_time_orders_drivers_regardless_of_input_order()
    {
        var newest = Driver("00000000-0000-0000-0000-000000000001", 100, AvailableSince.AddHours(2));
        var longestIdle = Driver("00000000-0000-0000-0000-000000000003", 900, AvailableSince);
        var middle = Driver("00000000-0000-0000-0000-000000000002", 500, AvailableSince.AddHours(1));

        var result = LongestIdleDriverRanking.Rank([newest, longestIdle, middle]);

        Assert.Equal([longestIdle, middle, newest], result);
    }

    [Fact]
    public void Equal_availability_time_orders_nearest_driver_first()
    {
        var farther = Driver("00000000-0000-0000-0000-000000000001", 200, AvailableSince);
        var nearer = Driver("00000000-0000-0000-0000-000000000002", 100, AvailableSince);

        var result = LongestIdleDriverRanking.Rank([farther, nearer]);

        Assert.Equal([nearer, farther], result);
    }

    [Fact]
    public void Equal_availability_time_and_distance_order_by_driver_id()
    {
        var higherId = Driver("00000000-0000-0000-0000-000000000002", 100, AvailableSince);
        var lowerId = Driver("00000000-0000-0000-0000-000000000001", 100, AvailableSince);

        var result = LongestIdleDriverRanking.Rank([higherId, lowerId]);

        Assert.Equal([lowerId, higherId], result);
    }

    [Fact]
    public void Empty_input_returns_empty_result()
    {
        var result = LongestIdleDriverRanking.Rank([]);

        Assert.Empty(result);
    }

    private static EligibleDriverView Driver(
        string driverId,
        double distanceMeters,
        DateTimeOffset availableSince) =>
        new(Guid.Parse(driverId), distanceMeters, availableSince, AvailableSince);
}
