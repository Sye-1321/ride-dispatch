namespace RideDispatch.Application.DriverFinancialStanding;

internal static class FinancialStandingValidation
{
    public const decimal MaximumCommissionBalance = 9_999_999_999.99m;

    public static void ValidateCommissionBalance(decimal commissionBalance)
    {
        if (commissionBalance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commissionBalance),
                "Commission balance must not be negative.");
        }

        if (commissionBalance > MaximumCommissionBalance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commissionBalance),
                $"Commission balance must not exceed {MaximumCommissionBalance}.");
        }

        if (decimal.Truncate(commissionBalance * 100m) != commissionBalance * 100m)
        {
            throw new ArgumentException(
                "Commission balance must have at most two decimal places.",
                nameof(commissionBalance));
        }
    }
}
