using RideDispatch.Application.Dispatch.Eligibility;

namespace RideDispatch.Application.Dispatch.Ranking;

public static class DriverRanking
{
    public static IReadOnlyList<EligibleDriverView> Rank(
        IEnumerable<EligibleDriverView> drivers,
        DispatchRankingPolicy policy) =>
        policy switch
        {
            DispatchRankingPolicy.Nearest => NearestDriverRanking.Rank(drivers),
            DispatchRankingPolicy.LongestIdle => LongestIdleDriverRanking.Rank(drivers),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported ranking policy."),
        };
}
