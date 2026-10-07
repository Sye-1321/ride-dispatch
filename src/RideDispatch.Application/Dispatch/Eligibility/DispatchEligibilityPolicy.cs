namespace RideDispatch.Application.Dispatch.Eligibility;

public sealed class DispatchEligibilityPolicy
{
    public DispatchEligibilityPolicy(
        double maxRadiusMeters,
        TimeSpan locationFreshness,
        decimal minimumCommissionBalance)
    {
        if (!double.IsFinite(maxRadiusMeters) || maxRadiusMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRadiusMeters),
                "Maximum dispatch radius must be finite and greater than zero.");
        }

        if (locationFreshness <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(locationFreshness),
                "Location freshness must be greater than zero.");
        }

        if (minimumCommissionBalance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumCommissionBalance),
                "Minimum commission balance must not be negative.");
        }

        MaxRadiusMeters = maxRadiusMeters;
        LocationFreshness = locationFreshness;
        MinimumCommissionBalance = minimumCommissionBalance;
    }

    public double MaxRadiusMeters { get; }

    public TimeSpan LocationFreshness { get; }

    public decimal MinimumCommissionBalance { get; }
}
