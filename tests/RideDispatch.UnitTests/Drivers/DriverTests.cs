using RideDispatch.Domain.Drivers;

namespace RideDispatch.UnitTests.Drivers;

public sealed class DriverTests
{
    [Fact]
    public void Create_starts_pending_offline_without_available_since()
    {
        var driver = Driver.Create("  Abebe Kebede  ");

        Assert.NotEqual(Guid.Empty, driver.Id);
        Assert.Equal("Abebe Kebede", driver.Name);
        Assert.Equal(DriverApprovalStatus.Pending, driver.ApprovalStatus);
        Assert.Equal(DriverOperationalStatus.Offline, driver.OperationalStatus);
        Assert.Null(driver.AvailableSince);
    }

    [Fact]
    public void Create_rejects_whitespace_only_name()
    {
        Assert.Throws<ArgumentException>(() => Driver.Create("   "));
    }

    [Fact]
    public void Create_rejects_name_longer_than_200_characters()
    {
        Assert.Throws<ArgumentException>(() => Driver.Create(new string('a', 201)));
    }

    [Fact]
    public void Pending_driver_cannot_become_available()
    {
        var driver = Driver.Create("Abebe Kebede");

        var changed = driver.TrySetOperationalStatus(
            DriverOperationalStatus.Available,
            DateTimeOffset.Parse("2026-10-04T08:00:00Z"));

        Assert.False(changed);
        Assert.Equal(DriverOperationalStatus.Offline, driver.OperationalStatus);
        Assert.Null(driver.AvailableSince);
    }

    [Fact]
    public void Approved_driver_becomes_available_at_supplied_time()
    {
        var driver = CreateApprovedDriver();
        var currentTime = DateTimeOffset.Parse("2026-10-04T08:00:00Z");

        var changed = driver.TrySetOperationalStatus(DriverOperationalStatus.Available, currentTime);

        Assert.True(changed);
        Assert.Equal(DriverOperationalStatus.Available, driver.OperationalStatus);
        Assert.Equal(currentTime, driver.AvailableSince);
    }

    [Fact]
    public void Setting_available_again_preserves_original_available_since()
    {
        var driver = CreateApprovedDriver();
        var originalTime = DateTimeOffset.Parse("2026-10-04T08:00:00Z");
        driver.TrySetOperationalStatus(DriverOperationalStatus.Available, originalTime);

        driver.TrySetOperationalStatus(
            DriverOperationalStatus.Available,
            originalTime.AddHours(1));

        Assert.Equal(originalTime, driver.AvailableSince);
    }

    [Theory]
    [InlineData(DriverOperationalStatus.Offline)]
    [InlineData(DriverOperationalStatus.Unavailable)]
    public void Leaving_available_clears_available_since(DriverOperationalStatus newStatus)
    {
        var driver = CreateAvailableDriver();

        driver.TrySetOperationalStatus(newStatus, DateTimeOffset.Parse("2026-10-04T09:00:00Z"));

        Assert.Equal(newStatus, driver.OperationalStatus);
        Assert.Null(driver.AvailableSince);
    }

    [Theory]
    [InlineData(DriverApprovalStatus.Pending)]
    [InlineData(DriverApprovalStatus.Suspended)]
    [InlineData(DriverApprovalStatus.Rejected)]
    public void Losing_approval_while_available_forces_offline(
        DriverApprovalStatus newApprovalStatus)
    {
        var driver = CreateAvailableDriver();

        driver.SetApprovalStatus(newApprovalStatus);

        Assert.Equal(newApprovalStatus, driver.ApprovalStatus);
        Assert.Equal(DriverOperationalStatus.Offline, driver.OperationalStatus);
        Assert.Null(driver.AvailableSince);
    }

    private static Driver CreateApprovedDriver()
    {
        var driver = Driver.Create("Abebe Kebede");
        driver.SetApprovalStatus(DriverApprovalStatus.Approved);
        return driver;
    }

    private static Driver CreateAvailableDriver()
    {
        var driver = CreateApprovedDriver();
        driver.TrySetOperationalStatus(
            DriverOperationalStatus.Available,
            DateTimeOffset.Parse("2026-10-04T08:00:00Z"));
        return driver;
    }
}
