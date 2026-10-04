namespace RideDispatch.Application.Drivers;

public sealed class ListDrivers(IDriverStore driverStore)
{
    public async Task<IReadOnlyList<DriverView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var drivers = await driverStore.ListAsync(cancellationToken);
        return drivers.Select(DriverView.FromDriver).ToList();
    }
}
