namespace RideDispatch.Application.DriverFinancialStanding;

public interface IDriverFinancialStandingStore
{
    Task<DriverFinancialStandingView> UpsertAsync(
        Guid driverId,
        decimal commissionBalance,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<DriverFinancialStandingView?> FindByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken);
}
