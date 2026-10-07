using RideDispatch.Application.Dispatch.Eligibility;

namespace RideDispatch.UnitTests.Dispatch.Eligibility;

public sealed class DispatchEligibilityPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Construction_rejects_invalid_radius(double radiusMeters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DispatchEligibilityPolicy(radiusMeters, TimeSpan.FromSeconds(60), 0.01m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Construction_rejects_invalid_freshness(double freshnessSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DispatchEligibilityPolicy(50_000, TimeSpan.FromSeconds(freshnessSeconds), 0.01m));
    }

    [Fact]
    public void Construction_rejects_negative_minimum_balance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DispatchEligibilityPolicy(50_000, TimeSpan.FromSeconds(60), -0.01m));
    }
}
