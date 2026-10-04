using RideDispatch.Application.DriverLocations;

namespace RideDispatch.UnitTests.DriverLocations;

public sealed class FindNearbyDriversTests
{
    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public async Task Execute_rejects_non_finite_coordinates(double latitude, double longitude)
    {
        var useCase = new FindNearbyDrivers(new StubDriverLocationStore());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(latitude, longitude, 1, CancellationToken.None));
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    public async Task Execute_rejects_coordinates_outside_legal_bounds(
        double latitude,
        double longitude)
    {
        var useCase = new FindNearbyDrivers(new StubDriverLocationStore());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(latitude, longitude, 1, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task Execute_rejects_invalid_radius(double radiusMeters)
    {
        var useCase = new FindNearbyDrivers(new StubDriverLocationStore());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            useCase.ExecuteAsync(0, 0, radiusMeters, CancellationToken.None));
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public async Task Execute_accepts_exact_coordinate_boundaries(
        double latitude,
        double longitude)
    {
        var store = new StubDriverLocationStore();
        var useCase = new FindNearbyDrivers(store);

        await useCase.ExecuteAsync(
            latitude,
            longitude,
            1,
            CancellationToken.None);

        Assert.True(store.FindWithinRadiusWasCalled);
    }

    private sealed class StubDriverLocationStore : IDriverLocationStore
    {
        public bool FindWithinRadiusWasCalled { get; private set; }

        public Task<DriverLocationView> UpsertAsync(
            Guid driverId,
            double latitude,
            double longitude,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DriverLocationView?> FindByDriverIdAsync(
            Guid driverId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<NearbyDriverView>> FindWithinRadiusAsync(
            double latitude,
            double longitude,
            double radiusMeters,
            CancellationToken cancellationToken)
        {
            FindWithinRadiusWasCalled = true;
            return Task.FromResult<IReadOnlyList<NearbyDriverView>>([]);
        }
    }
}
