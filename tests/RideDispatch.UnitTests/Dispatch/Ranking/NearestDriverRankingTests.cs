using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.Dispatch.Ranking;

namespace RideDispatch.UnitTests.Dispatch.Ranking;

public sealed class NearestDriverRankingTests
{
    private static readonly DateTimeOffset AvailableSince =
        new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Distance_orders_drivers_regardless_of_input_order()
    {
        var farthest = Driver("00000000-0000-0000-0000-000000000001", 900, AvailableSince);
        var nearest = Driver("00000000-0000-0000-0000-000000000003", 100, AvailableSince);
        var middle = Driver("00000000-0000-0000-0000-000000000002", 500, AvailableSince);

        var result = NearestDriverRanking.Rank([farthest, nearest, middle]);

        Assert.Equal([nearest, middle, farthest], result);
    }

    [Fact]
    public void Equal_distance_orders_longer_available_driver_first()
    {
        var recentlyAvailable = Driver(
            "00000000-0000-0000-0000-000000000001",
            100,
            AvailableSince.AddMinutes(30));
        var longerAvailable = Driver(
            "00000000-0000-0000-0000-000000000002",
            100,
            AvailableSince);

        var result = NearestDriverRanking.Rank([recentlyAvailable, longerAvailable]);

        Assert.Equal([longerAvailable, recentlyAvailable], result);
    }

    [Fact]
    public void Equal_distance_and_availability_order_by_driver_id()
    {
        var higherId = Driver("00000000-0000-0000-0000-000000000002", 100, AvailableSince);
        var lowerId = Driver("00000000-0000-0000-0000-000000000001", 100, AvailableSince);

        var result = NearestDriverRanking.Rank([higherId, lowerId]);

        Assert.Equal([lowerId, higherId], result);
    }

    [Fact]
    public void Empty_input_returns_empty_result()
    {
        var result = NearestDriverRanking.Rank([]);

        Assert.Empty(result);
    }

    private static EligibleDriverView Driver(
        string driverId,
        double distanceMeters,
        DateTimeOffset availableSince) =>
        new(Guid.Parse(driverId), distanceMeters, availableSince, AvailableSince);
}
