using RideDispatch.Application.DriverFinancialStanding;
using RideDispatch.Application.Drivers;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.UnitTests.DriverFinancialStanding;

public sealed class UpdateDriverFinancialStandingTests
{
    [Theory]
    [InlineData("-1")]
    [InlineData("100.123")]
    [InlineData("10000000000")]
    public async Task Execute_rejects_invalid_balances(string balanceText)
    {
        var driver = Driver.Create("Driver");
        var standingStore = new CapturingFinancialStandingStore();
        var useCase = CreateUseCase(driver, standingStore, DateTimeOffset.UtcNow);
        var balance = decimal.Parse(balanceText, System.Globalization.CultureInfo.InvariantCulture);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(driver.Id, balance, CancellationToken.None));

        Assert.False(standingStore.UpsertWasCalled);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1250.75")]
    public async Task Execute_accepts_valid_balances(string balanceText)
    {
        var driver = Driver.Create("Driver");
        var standingStore = new CapturingFinancialStandingStore();
        var useCase = CreateUseCase(driver, standingStore, DateTimeOffset.UtcNow);
        var balance = decimal.Parse(balanceText, System.Globalization.CultureInfo.InvariantCulture);

        var result = await useCase.ExecuteAsync(driver.Id, balance, CancellationToken.None);

        Assert.Equal(UpdateDriverFinancialStandingOutcome.Success, result.Outcome);
        Assert.Equal(balance, result.FinancialStanding!.CommissionBalance);
    }

    [Fact]
    public async Task Execute_passes_driver_balance_and_time_provider_time_to_persistence()
    {
        var driver = Driver.Create("Driver");
        var currentTime = new DateTimeOffset(2026, 10, 4, 12, 34, 56, TimeSpan.Zero);
        var standingStore = new CapturingFinancialStandingStore();
        var useCase = CreateUseCase(driver, standingStore, currentTime);

        await useCase.ExecuteAsync(driver.Id, 250.50m, CancellationToken.None);

        Assert.True(standingStore.UpsertWasCalled);
        Assert.Equal(driver.Id, standingStore.DriverId);
        Assert.Equal(250.50m, standingStore.CommissionBalance);
        Assert.Equal(currentTime, standingStore.UpdatedAt);
    }

    private static UpdateDriverFinancialStanding CreateUseCase(
        Driver driver,
        CapturingFinancialStandingStore standingStore,
        DateTimeOffset currentTime) =>
        new(new ExistingDriverStore(driver), standingStore, new FixedTimeProvider(currentTime));

    private sealed class ExistingDriverStore(Driver driver) : IDriverStore
    {
        public Task<Driver?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Driver?>(id == driver.Id ? driver : null);

        public Task AddAsync(Driver driver, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Driver>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingFinancialStandingStore : IDriverFinancialStandingStore
    {
        public bool UpsertWasCalled { get; private set; }

        public Guid DriverId { get; private set; }

        public decimal CommissionBalance { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Task<DriverFinancialStandingView> UpsertAsync(
            Guid driverId,
            decimal commissionBalance,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken)
        {
            UpsertWasCalled = true;
            DriverId = driverId;
            CommissionBalance = commissionBalance;
            UpdatedAt = updatedAt;

            return Task.FromResult(new DriverFinancialStandingView(
                driverId,
                commissionBalance,
                updatedAt));
        }

        public Task<DriverFinancialStandingView?> FindByDriverIdAsync(
            Guid driverId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
