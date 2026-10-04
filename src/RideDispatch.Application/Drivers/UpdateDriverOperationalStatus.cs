using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public enum UpdateDriverOperationalStatusOutcome
{
    Success,
    DriverNotFound,
    DriverNotApproved,
}

public sealed record UpdateDriverOperationalStatusResult(
    UpdateDriverOperationalStatusOutcome Outcome,
    DriverView? Driver = null);

public sealed class UpdateDriverOperationalStatus(IDriverStore driverStore, TimeProvider timeProvider)
{
    public async Task<UpdateDriverOperationalStatusResult> ExecuteAsync(
        Guid id,
        DriverOperationalStatus status,
        CancellationToken cancellationToken)
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

        await driverStore.SaveChangesAsync(cancellationToken);
        return new(UpdateDriverOperationalStatusOutcome.Success, DriverView.FromDriver(driver));
    }
}
