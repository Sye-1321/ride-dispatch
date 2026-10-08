using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.Dispatch.Ranking;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Dispatch.Eligibility;

public sealed class FindEligibleDriversTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly DispatchEligibilityPolicy Policy =
        new(50_000, TimeSpan.FromSeconds(60), 0.01m);

    [Fact]
    public async Task Missing_ride_returns_not_found_without_querying_candidates()
    {
        var candidateStore = new CapturingCandidateStore([]);
        var useCase = new FindEligibleDrivers(
            new StubRideRequestStore(null),
            candidateStore,
            Policy,
            new FixedTimeProvider(CurrentTime));

        var result = await useCase.ExecuteAsync(
            Guid.CreateVersion7(),
            DispatchRankingPolicy.Nearest,
            CancellationToken.None);

        Assert.Equal(FindEligibleDriversOutcome.RideRequestNotFound, result.Outcome);
        Assert.False(candidateStore.WasCalled);
    }

    [Fact]
    public async Task Query_uses_ride_pickup_and_configured_radius()
    {
        var rideRequest = CreateRideRequest();
        var candidateStore = new CapturingCandidateStore([]);
        var useCase = new FindEligibleDrivers(
            new StubRideRequestStore(rideRequest),
            candidateStore,
            Policy,
            new FixedTimeProvider(CurrentTime));

        await useCase.ExecuteAsync(
            rideRequest.Id,
            DispatchRankingPolicy.Nearest,
            CancellationToken.None);

        Assert.True(candidateStore.WasCalled);
        Assert.Equal(rideRequest.PickupLatitude, candidateStore.PickupLatitude);
        Assert.Equal(rideRequest.PickupLongitude, candidateStore.PickupLongitude);
        Assert.Equal(Policy.MaxRadiusMeters, candidateStore.RadiusMeters);
    }

    [Fact]
    public async Task Requested_ranking_policy_is_applied_after_eligibility_filtering()
    {
        var rideRequest = CreateRideRequest();
        var higherId = Guid.Parse("0199b631-0000-7000-8000-000000000002");
        var lowerId = Guid.Parse("0199b631-0000-7000-8000-000000000001");
        var staleId = Guid.Parse("0199b631-0000-7000-8000-000000000003");
        var candidateStore = new CapturingCandidateStore(
        [
            Candidate(higherId, CurrentTime, CurrentTime.AddMinutes(-30), 100),
            Candidate(staleId, CurrentTime.AddSeconds(-61), CurrentTime.AddHours(-3), 50),
            Candidate(lowerId, CurrentTime.AddSeconds(-60), CurrentTime.AddHours(-2), 200),
        ]);
        var timeProvider = new FixedTimeProvider(CurrentTime);
        var useCase = new FindEligibleDrivers(
            new StubRideRequestStore(rideRequest),
            candidateStore,
            Policy,
            timeProvider);

        var result = await useCase.ExecuteAsync(
            rideRequest.Id,
            DispatchRankingPolicy.LongestIdle,
            CancellationToken.None);

        Assert.Equal(FindEligibleDriversOutcome.Success, result.Outcome);
        Assert.Equal([lowerId, higherId], result.Drivers!.Select(driver => driver.DriverId));
        Assert.Equal(1, timeProvider.GetUtcNowCallCount);
    }

    private static RideRequest CreateRideRequest() =>
        RideRequest.Create(
            null,
            "Hana Gebru",
            "+251911000000",
            BookingSource.CallCenter,
            RideTiming.Immediate,
            9.03,
            38.74,
            9.01,
            38.78,
            VehicleType.Standard,
            null,
            1200,
            CurrentTime);

    private static DispatchCandidateSnapshot Candidate(
        Guid driverId,
        DateTimeOffset locationRecordedAt,
        DateTimeOffset availableSince,
        double distanceMeters) =>
        new(
            driverId,
            DriverApprovalStatus.Approved,
            DriverOperationalStatus.Available,
            availableSince,
            VehicleType.Standard,
            locationRecordedAt,
            distanceMeters,
            1m);

    private sealed class StubRideRequestStore(RideRequest? rideRequest) : IRideRequestStore
    {
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(rideRequest?.Id == id ? rideRequest : null);

        public Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingCandidateStore(
        IReadOnlyList<DispatchCandidateSnapshot> candidates) : IDispatchCandidateStore
    {
        public bool WasCalled { get; private set; }

        public double PickupLatitude { get; private set; }

        public double PickupLongitude { get; private set; }

        public double RadiusMeters { get; private set; }

        public Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
            double pickupLatitude,
            double pickupLongitude,
            double radiusMeters,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            PickupLatitude = pickupLatitude;
            PickupLongitude = pickupLongitude;
            RadiusMeters = radiusMeters;
            return Task.FromResult(candidates);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public int GetUtcNowCallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            GetUtcNowCallCount++;
            return currentTime;
        }
    }
}
