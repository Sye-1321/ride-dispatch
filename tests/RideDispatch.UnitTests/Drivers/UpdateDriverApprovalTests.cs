using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.UnitTests.Drivers;

public sealed class UpdateDriverApprovalTests
{
    [Fact]
    public async Task Missing_driver_returns_driver_not_found()
    {
        var store = new FakeDriverStore(null);
        var useCase = new UpdateDriverApproval(store);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), DriverApprovalStatus.Approved, CancellationToken.None);

        Assert.Equal(UpdateDriverApprovalOutcome.DriverNotFound, result.Outcome);
        Assert.Null(result.Driver);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Successful_approval_update_persists_and_returns_view()
    {
        var driver = Driver.Create("Abebe Kebede");
        var store = new FakeDriverStore(driver);
        var useCase = new UpdateDriverApproval(store);

        var result = await useCase.ExecuteAsync(driver.Id, DriverApprovalStatus.Approved, CancellationToken.None);

        Assert.Equal(UpdateDriverApprovalOutcome.Success, result.Outcome);
        Assert.NotNull(result.Driver);
        Assert.Equal(driver.Id, result.Driver.Id);
        Assert.Equal(DriverApprovalStatus.Approved, result.Driver.ApprovalStatus);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Concurrency_conflict_reloads_authoritative_state_and_demotes_available_driver()
    {
        var driver = Driver.Create("Abebe Kebede");
        driver.SetApprovalStatus(DriverApprovalStatus.Approved);

        // Simulate concurrent update on the same driver identity: driver became Available
        var store = new FakeDriverStore(driver)
        {
            ThrowConcurrencyOnSaveCount = 1,
            OnReload = d =>
            {
                d.SetApprovalStatus(DriverApprovalStatus.Approved);
                var becameAvailable = d.TrySetOperationalStatus(
                    DriverOperationalStatus.Available,
                    DateTimeOffset.UtcNow);
                Assert.True(becameAvailable);
            },
        };
        var useCase = new UpdateDriverApproval(store);

        var result = await useCase.ExecuteAsync(driver.Id, DriverApprovalStatus.Suspended, CancellationToken.None);

        Assert.Equal(UpdateDriverApprovalOutcome.Success, result.Outcome);
        Assert.NotNull(result.Driver);
        Assert.Equal(driver.Id, result.Driver.Id);
        Assert.Equal(DriverApprovalStatus.Suspended, result.Driver.ApprovalStatus);
        Assert.Equal(DriverOperationalStatus.Offline, result.Driver.OperationalStatus);
        Assert.Null(result.Driver.AvailableSince);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public async Task Continued_contention_exhausts_retries_and_returns_conflict()
    {
        var driver = Driver.Create("Abebe Kebede");
        var store = new FakeDriverStore(driver)
        {
            ThrowConcurrencyOnSaveCount = 2,
        };
        var useCase = new UpdateDriverApproval(store);

        var result = await useCase.ExecuteAsync(driver.Id, DriverApprovalStatus.Suspended, CancellationToken.None);

        Assert.Equal(UpdateDriverApprovalOutcome.Conflict, result.Outcome);
        Assert.Null(result.Driver);
        Assert.Equal(2, store.SaveCount);
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
