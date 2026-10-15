using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.UnitTests.Drivers;

public sealed class UpdateDriverOperationalStatusTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-15T12:00:00Z");

    [Fact]
    public async Task Missing_driver_returns_not_found()
    {
        var store = new FakeDriverStore(null);
        var clock = new FixedTimeProvider(Now);
        var useCase = new UpdateDriverOperationalStatus(store, clock);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), DriverOperationalStatus.Available, CancellationToken.None);

        Assert.Equal(UpdateDriverOperationalStatusOutcome.DriverNotFound, result.Outcome);
        Assert.Null(result.Driver);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Unapproved_driver_cannot_become_available()
    {
        var driver = Driver.Create("Abebe Kebede");
        var store = new FakeDriverStore(driver);
        var clock = new FixedTimeProvider(Now);
        var useCase = new UpdateDriverOperationalStatus(store, clock);

        var result = await useCase.ExecuteAsync(driver.Id, DriverOperationalStatus.Available, CancellationToken.None);

        Assert.Equal(UpdateDriverOperationalStatusOutcome.DriverNotApproved, result.Outcome);
        Assert.Null(result.Driver);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Approved_driver_can_become_available()
    {
        var driver = Driver.Create("Abebe Kebede");
        driver.SetApprovalStatus(DriverApprovalStatus.Approved);
        var store = new FakeDriverStore(driver);
        var clock = new FixedTimeProvider(Now);
        var useCase = new UpdateDriverOperationalStatus(store, clock);

        var result = await useCase.ExecuteAsync(driver.Id, DriverOperationalStatus.Available, CancellationToken.None);

        Assert.Equal(UpdateDriverOperationalStatusOutcome.Success, result.Outcome);
        Assert.NotNull(result.Driver);
        Assert.Equal(driver.Id, result.Driver.Id);
        Assert.Equal(DriverOperationalStatus.Available, result.Driver.OperationalStatus);
        Assert.Equal(Now, result.Driver.AvailableSince);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Concurrency_conflict_when_approval_revoked_returns_driver_not_approved()
    {
        var driver = Driver.Create("Abebe Kebede");
        driver.SetApprovalStatus(DriverApprovalStatus.Approved);

        // Simulate concurrent revocation on the same driver identity: approval status changed to Suspended
        var store = new FakeDriverStore(driver)
        {
            ThrowConcurrencyOnSaveCount = 1,
            OnReload = d => d.SetApprovalStatus(DriverApprovalStatus.Suspended),
        };
        var clock = new FixedTimeProvider(Now);
        var useCase = new UpdateDriverOperationalStatus(store, clock);

        var result = await useCase.ExecuteAsync(driver.Id, DriverOperationalStatus.Available, CancellationToken.None);

        Assert.Equal(UpdateDriverOperationalStatusOutcome.DriverNotApproved, result.Outcome);
        Assert.Null(result.Driver);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Concurrency_conflict_when_still_approved_retries_and_succeeds()
    {
        var driver = Driver.Create("Abebe Kebede");
        driver.SetApprovalStatus(DriverApprovalStatus.Approved);

        // Simulate concurrent update where driver remains approved on reload (same identity)
        var store = new FakeDriverStore(driver)
        {
            ThrowConcurrencyOnSaveCount = 1,
        };
        var clock = new FixedTimeProvider(Now);
        var useCase = new UpdateDriverOperationalStatus(store, clock);

        var result = await useCase.ExecuteAsync(driver.Id, DriverOperationalStatus.Available, CancellationToken.None);

        Assert.Equal(UpdateDriverOperationalStatusOutcome.Success, result.Outcome);
        Assert.NotNull(result.Driver);
        Assert.Equal(driver.Id, result.Driver.Id);
        Assert.Equal(DriverOperationalStatus.Available, result.Driver.OperationalStatus);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public async Task Continued_contention_exhausts_retries_and_returns_conflict()
    {
        var driver = Driver.Create("Abebe Kebede");
        driver.SetApprovalStatus(DriverApprovalStatus.Approved);

        var store = new FakeDriverStore(driver)
        {
            ThrowConcurrencyOnSaveCount = 2,
        };
        var clock = new FixedTimeProvider(Now);
        var useCase = new UpdateDriverOperationalStatus(store, clock);

        var result = await useCase.ExecuteAsync(driver.Id, DriverOperationalStatus.Available, CancellationToken.None);

        Assert.Equal(UpdateDriverOperationalStatusOutcome.Conflict, result.Outcome);
        Assert.Null(result.Driver);
        Assert.Equal(2, store.SaveCount);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDriverStore(Driver? initialDriver) : IDriverStore
    {
        private readonly Driver? driver = initialDriver;

        public Action<Driver>? OnReload { get; set; }
        public int ThrowConcurrencyOnSaveCount { get; set; }
        public int SaveCount { get; private set; }

        public Task AddAsync(Driver driver, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Driver?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            if (driver is not null && driver.Id == id)
            {
                if (SaveCount > 0)
                {
                    OnReload?.Invoke(driver);
                }

                return Task.FromResult<Driver?>(driver);
            }

            return Task.FromResult<Driver?>(null);
        }

        public Task<IReadOnlyList<Driver>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (SaveCount <= ThrowConcurrencyOnSaveCount)
            {
                throw new DriverStateConcurrencyException(new Exception("Simulated concurrency conflict."));
            }

            return Task.CompletedTask;
        }
    }
}
