namespace RideDispatch.Application.DriverFinancialStanding;

public sealed record DriverFinancialStandingView(
    Guid DriverId,
    decimal CommissionBalance,
    DateTimeOffset UpdatedAt);
