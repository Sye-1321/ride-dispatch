namespace RideDispatch.Application.DriverFinancialStanding;

public sealed class GetDriverFinancialStanding(IDriverFinancialStandingStore financialStandingStore)
{
    public Task<DriverFinancialStandingView?> ExecuteAsync(
        Guid driverId,
        CancellationToken cancellationToken) =>
        financialStandingStore.FindByDriverIdAsync(driverId, cancellationToken);
}
