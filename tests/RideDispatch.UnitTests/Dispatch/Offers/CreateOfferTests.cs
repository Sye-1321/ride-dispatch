using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Dispatch.Offers;

public sealed class CreateOfferTests
{
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_run_does_not_persist()
    {
        var store = new StubOfferStore();
        var result = await CreateUseCase(null, store).ExecuteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(CreateOfferOutcome.AllocationRunNotFound, result.Outcome);
        Assert.Equal(0, store.AddCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Run_without_recommendation_does_not_persist()
    {
        var run = AllocationRun.Create(Guid.NewGuid(), DispatchRankingPolicy.Nearest, null, CurrentTime);
        var store = new StubOfferStore();
        var result = await CreateUseCase(run, store).ExecuteAsync(run.Id, CancellationToken.None);
        Assert.Equal(CreateOfferOutcome.NoRecommendedDriver, result.Outcome);
        Assert.Equal(0, store.AddCount);
    }

    [Fact]
    public async Task Recommended_driver_and_policy_ttl_are_used_and_saved_once()
    {
        var driverId = Guid.NewGuid();
        var run = AllocationRun.Create(Guid.NewGuid(), DispatchRankingPolicy.Nearest, driverId, CurrentTime);
        var store = new StubOfferStore();
        var result = await CreateUseCase(run, store).ExecuteAsync(run.Id, CancellationToken.None);
        Assert.Equal(CreateOfferOutcome.Success, result.Outcome);
        Assert.Equal(driverId, store.Added!.DriverId);
        Assert.Equal(OfferSelectionMode.Recommended, store.Added.SelectionMode);
        Assert.Null(store.Added.OverrideReason);
        Assert.Equal(TimeSpan.FromSeconds(37), store.Added.ExpiresAt - store.Added.CreatedAt);
        Assert.Equal(1, store.AddCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Existing_offer_returns_duplicate_without_add_or_save()
    {
        var driverId = Guid.NewGuid();
        var run = AllocationRun.Create(Guid.NewGuid(), DispatchRankingPolicy.Nearest, driverId, CurrentTime);
        var store = new StubOfferStore { Existing = Offer.Create(run.Id, driverId, TimeSpan.FromSeconds(1), CurrentTime) };
        var result = await CreateUseCase(run, store).ExecuteAsync(run.Id, CancellationToken.None);
        Assert.Equal(CreateOfferOutcome.OfferAlreadyExists, result.Outcome);
        Assert.Equal(0, store.AddCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Unique_constraint_race_is_mapped_to_duplicate()
    {
        var driverId = Guid.NewGuid();
        var run = AllocationRun.Create(Guid.NewGuid(), DispatchRankingPolicy.Nearest, driverId, CurrentTime);
        var store = new StubOfferStore { ThrowDuplicateOnSave = true };
        var result = await CreateUseCase(run, store).ExecuteAsync(run.Id, CancellationToken.None);
        Assert.Equal(CreateOfferOutcome.OfferAlreadyExists, result.Outcome);
    }

    [Fact]
    public async Task Scheduled_ride_at_exact_pickup_creates_offer_successfully()
    {
        var rideRequest = CreateScheduledRide(CurrentTime);
        var driverId = Guid.NewGuid();
        var run = AllocationRun.Create(rideRequest.Id, DispatchRankingPolicy.Nearest, driverId, CurrentTime);
        var store = new StubOfferStore();
        var result = await CreateUseCase(run, store, rideRequest, CurrentTime).ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(CreateOfferOutcome.Success, result.Outcome);
        Assert.NotNull(result.Offer);
        Assert.Equal(1, store.AddCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Scheduled_ride_after_pickup_is_rejected_without_persisting()
    {
        var rideRequest = CreateScheduledRide(CurrentTime.AddMinutes(-1));
        var driverId = Guid.NewGuid();
        var run = AllocationRun.Create(rideRequest.Id, DispatchRankingPolicy.Nearest, driverId, CurrentTime.AddMinutes(-2));
        var store = new StubOfferStore();
        var result = await CreateUseCase(run, store, rideRequest, CurrentTime).ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(CreateOfferOutcome.ScheduledPickupElapsed, result.Outcome);
        Assert.Null(result.Offer);
        Assert.Equal(0, store.AddCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Immediate_ride_creates_offer_regardless_of_time_elapsed()
    {
        var rideRequest = RideRequest.Create(
            null, "Immediate Passenger", "+251911000000", BookingSource.CallCenter,
            RideTiming.Immediate, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
            null, 1200, CurrentTime.AddHours(-1));
        var driverId = Guid.NewGuid();
        var run = AllocationRun.Create(rideRequest.Id, DispatchRankingPolicy.Nearest, driverId, CurrentTime);
        var store = new StubOfferStore();
        var result = await CreateUseCase(run, store, rideRequest, CurrentTime).ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(CreateOfferOutcome.Success, result.Outcome);
        Assert.Equal(1, store.AddCount);
        Assert.Equal(1, store.SaveCount);
    }

    private static CreateOffer CreateUseCase(
        AllocationRun? run,
        StubOfferStore store,
        RideRequest? rideRequest = null,
        DateTimeOffset? now = null)
    {
        var currentTime = now ?? CurrentTime;
        var defaultRide = run is not null
            ? RideRequest.Create(
                null, "Default Passenger", "+251911000000", BookingSource.CallCenter,
                RideTiming.Immediate, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
                null, 1200, currentTime)
            : null;
        var actualRide = rideRequest ?? defaultRide;
        return new CreateOffer(
            new StubAllocationRunStore(run),
            new StubRideRequestStore(actualRide),
            store,
            new OfferPolicy(TimeSpan.FromSeconds(37)),
            new CountingTimeProvider(currentTime));
    }

    private static RideRequest CreateScheduledRide(DateTimeOffset pickupAt)
    {
        var ride = RideRequest.Create(
            null, "Scheduled Passenger", "+251911000000", BookingSource.CallCenter,
            RideTiming.Scheduled, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
            pickupAt, 1200, pickupAt.AddMinutes(-30));
        ride.ActivateDispatch(pickupAt.AddMinutes(-20), TimeSpan.FromMinutes(30));
        return ride;
    }

    private sealed class StubRideRequestStore(RideRequest? rideRequest) : IRideRequestStore
    {
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(rideRequest);
        public Task<RideRequest?> FindByIdTrackedAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(RideRequest value, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class StubAllocationRunStore(AllocationRun? run) : IAllocationRunStore
    {
        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(run?.Id == id ? run : null);
        public Task<AllocationRun?> FindBySourceOfferIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(AllocationRun allocationRun, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubOfferStore : IOfferStore
    {
        public Offer? Existing { get; init; }
        public Offer? Added { get; private set; }
        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool ThrowDuplicateOnSave { get; init; }
        public Task AddAsync(Offer offer, CancellationToken ct) { Added = offer; AddCount++; return Task.CompletedTask; }
        public Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Existing);
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return ThrowDuplicateOnSave ? Task.FromException(new OfferAlreadyExistsException(new Exception())) : Task.CompletedTask;
        }
    }

    private sealed class CountingTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
