using RideDispatch.Application.Drivers;

namespace RideDispatch.Application.DriverFinancialStanding;

public enum UpdateDriverFinancialStandingOutcome
{
    Success,
    DriverNotFound,
}

public sealed record UpdateDriverFinancialStandingResult(
    UpdateDriverFinancialStandingOutcome Outcome,
    DriverFinancialStandingView? FinancialStanding = null);

public sealed class UpdateDriverFinancialStanding(
    IDriverStore driverStore,
    IDriverFinancialStandingStore financialStandingStore,
    TimeProvider timeProvider)
{
    public async Task<UpdateDriverFinancialStandingResult> ExecuteAsync(
        Guid driverId,
        decimal commissionBalance,
        CancellationToken cancellationToken)
    {
        if (await driverStore.FindAsync(driverId, cancellationToken) is null)
        {
            return new(UpdateDriverFinancialStandingOutcome.DriverNotFound);
        }

        FinancialStandingValidation.ValidateCommissionBalance(commissionBalance);

        var financialStanding = await financialStandingStore.UpsertAsync(
            driverId,
            commissionBalance,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return new(UpdateDriverFinancialStandingOutcome.Success, financialStanding);
    }
}
