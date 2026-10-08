using RideDispatch.Application.Dispatch.Eligibility;

namespace RideDispatch.Application.Dispatch.Ranking;

public static class NearestDriverRanking
{
    public static IReadOnlyList<EligibleDriverView> Rank(IEnumerable<EligibleDriverView> drivers) =>
        drivers
            .OrderBy(driver => driver.DistanceMeters)
            .ThenBy(driver => driver.AvailableSince)
            .ThenBy(driver => driver.DriverId)
            .ToList();
}
