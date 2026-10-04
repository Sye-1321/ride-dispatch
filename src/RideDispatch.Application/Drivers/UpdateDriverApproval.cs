using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public sealed class UpdateDriverApproval(IDriverStore driverStore)
{
    public async Task<DriverView?> ExecuteAsync(
        Guid id,
        DriverApprovalStatus status,
        CancellationToken cancellationToken)
    {
        var driver = await driverStore.FindAsync(id, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        driver.SetApprovalStatus(status);
        await driverStore.SaveChangesAsync(cancellationToken);
        return DriverView.FromDriver(driver);
    }
}
