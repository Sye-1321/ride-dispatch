namespace RideDispatch.Application.Drivers;

public sealed class GetDriver(IDriverStore driverStore)
{
    public async Task<DriverView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var driver = await driverStore.FindAsync(id, cancellationToken);
        return driver is null ? null : DriverView.FromDriver(driver);
    }
}
