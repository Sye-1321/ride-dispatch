using RideDispatch.Application.Dispatch.Eligibility;

namespace RideDispatch.Application.Dispatch.Ranking;

public static class LongestIdleDriverRanking
{
    public static IReadOnlyList<EligibleDriverView> Rank(IEnumerable<EligibleDriverView> drivers) =>
        drivers
            .OrderBy(driver => driver.AvailableSince)
            .ThenBy(driver => driver.DistanceMeters)
            .ThenBy(driver => driver.DriverId)
            .ToList();
}
