using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public sealed class CreateDriver(IDriverStore driverStore)
{
    public async Task<DriverView> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var driver = Driver.Create(name);
        await driverStore.AddAsync(driver, cancellationToken);
        await driverStore.SaveChangesAsync(cancellationToken);
        return DriverView.FromDriver(driver);
    }
}
