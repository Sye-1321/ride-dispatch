using RideDispatch.Domain.Drivers;

namespace RideDispatch.Application.Drivers;

public sealed record DriverView(
    Guid Id,
    string Name,
    DriverApprovalStatus ApprovalStatus,
    DriverOperationalStatus OperationalStatus,
    DateTimeOffset? AvailableSince)
{
    public static DriverView FromDriver(Driver driver) =>
        new(
            driver.Id,
            driver.Name,
            driver.ApprovalStatus,
            driver.OperationalStatus,
            driver.AvailableSince);
}
