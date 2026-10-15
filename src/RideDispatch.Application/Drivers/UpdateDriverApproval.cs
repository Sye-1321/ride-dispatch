using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public enum UpdateDriverApprovalOutcome
{
    Success,
    DriverNotFound,
    Conflict,
}

public sealed record UpdateDriverApprovalResult(
    UpdateDriverApprovalOutcome Outcome,
    DriverView? Driver = null);

public sealed class UpdateDriverApproval(IDriverStore driverStore)
{
    private const int MaxAttempts = 2;

    public async Task<UpdateDriverApprovalResult> ExecuteAsync(
        Guid id,
        DriverApprovalStatus status,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var driver = await driverStore.FindAsync(id, cancellationToken);
            if (driver is null)
            {
                return new(UpdateDriverApprovalOutcome.DriverNotFound);
            }

            driver.SetApprovalStatus(status);

            try
            {
                await driverStore.SaveChangesAsync(cancellationToken);
                return new(UpdateDriverApprovalOutcome.Success, DriverView.FromDriver(driver));
            }
            catch (DriverStateConcurrencyException) when (attempt < MaxAttempts)
            {
            }
            catch (DriverStateConcurrencyException)
            {
                return new(UpdateDriverApprovalOutcome.Conflict);
            }
        }

        return new(UpdateDriverApprovalOutcome.Conflict);
    }
}
