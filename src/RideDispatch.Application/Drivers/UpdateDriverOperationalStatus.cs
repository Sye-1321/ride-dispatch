using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public enum UpdateDriverOperationalStatusOutcome
{
    Success,
    DriverNotFound,
    DriverNotApproved,
    Conflict,
}

public sealed record UpdateDriverOperationalStatusResult(
    UpdateDriverOperationalStatusOutcome Outcome,
    DriverView? Driver = null);

public sealed class UpdateDriverOperationalStatus(IDriverStore driverStore, TimeProvider timeProvider)
{
    private const int MaxAttempts = 2;

    public async Task<UpdateDriverOperationalStatusResult> ExecuteAsync(
        Guid id,
        DriverOperationalStatus status,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var driver = await driverStore.FindAsync(id, cancellationToken);
            if (driver is null)
            {
                return new(UpdateDriverOperationalStatusOutcome.DriverNotFound);
            }

            if (!driver.TrySetOperationalStatus(status, timeProvider.GetUtcNow()))
            {
                return new(UpdateDriverOperationalStatusOutcome.DriverNotApproved);
            }

            try
            {
                await driverStore.SaveChangesAsync(cancellationToken);
                return new(UpdateDriverOperationalStatusOutcome.Success, DriverView.FromDriver(driver));
            }
            catch (DriverStateConcurrencyException) when (attempt < MaxAttempts)
            {
            }
            catch (DriverStateConcurrencyException)
            {
                return new(UpdateDriverOperationalStatusOutcome.Conflict);
            }
        }

        return new(UpdateDriverOperationalStatusOutcome.Conflict);
    }
}
