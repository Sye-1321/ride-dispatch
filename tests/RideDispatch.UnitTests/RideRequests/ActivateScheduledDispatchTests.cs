using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.RideRequests;

public sealed class ActivateScheduledDispatchTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PickupAt = CreatedAt.AddHours(1);
    private static readonly ScheduledDispatchPolicy Policy = new(TimeSpan.FromMinutes(15));

    [Fact]
    public async Task Missing_and_immediate_rides_do_not_read_clock_or_save()
    {
        var clock = new CountingTimeProvider(PickupAt.AddMinutes(-15));
        var missingStore = new StubStore(null);
        var missing = await new ActivateScheduledDispatch(missingStore, Policy, clock)
            .ExecuteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(ActivateScheduledDispatchOutcome.RideRequestNotFound, missing.Outcome);

        var immediateStore = new StubStore(CreateRide(RideTiming.Immediate));
        var immediate = await new ActivateScheduledDispatch(immediateStore, Policy, clock)
            .ExecuteAsync(immediateStore.RideRequest!.Id, CancellationToken.None);
        Assert.Equal(ActivateScheduledDispatchOutcome.RideNotScheduled, immediate.Outcome);
        Assert.Equal(0, clock.ReadCount);
        Assert.Equal(0, immediateStore.SaveCount);
    }

    [Theory]
    [InlineData(-16, ActivateScheduledDispatchOutcome.ActivationTooEarly, 0)]
    [InlineData(-15, ActivateScheduledDispatchOutcome.Success, 1)]
    [InlineData(-5, ActivateScheduledDispatchOutcome.Success, 1)]
    [InlineData(0, ActivateScheduledDispatchOutcome.Success, 1)]
    [InlineData(1, ActivateScheduledDispatchOutcome.ScheduledPickupElapsed, 0)]
    public async Task Applies_inclusive_activation_window(
        int minutesFromPickup,
        ActivateScheduledDispatchOutcome expected,
        int expectedSaves)
    {
        var ride = CreateRide(RideTiming.Scheduled);
        var store = new StubStore(ride);
        var clock = new CountingTimeProvider(PickupAt.AddMinutes(minutesFromPickup));

        var result = await new ActivateScheduledDispatch(store, Policy, clock)
            .ExecuteAsync(ride.Id, CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, clock.ReadCount);
        Assert.Equal(expectedSaves, store.SaveCount);
    }

    [Fact]
    public async Task Retry_returns_original_activation_without_reading_clock()
    {
        var ride = CreateRide(RideTiming.Scheduled);
        var original = PickupAt.AddMinutes(-10);
        ride.ActivateDispatch(original, Policy.ActivationLeadTime);
        var store = new StubStore(ride);
        var clock = new CountingTimeProvider(PickupAt);

        var result = await new ActivateScheduledDispatch(store, Policy, clock)
            .ExecuteAsync(ride.Id, CancellationToken.None);

        Assert.Equal(ActivateScheduledDispatchOutcome.AlreadyActivated, result.Outcome);
        Assert.Equal(original, result.RideRequest!.DispatchActivatedAt);
        Assert.Equal(0, clock.ReadCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Concurrency_loss_reloads_winner_as_already_activated()
    {
        var attemptedRide = CreateRide(RideTiming.Scheduled);
        var persistedWinner = CreateRide(RideTiming.Scheduled);
        var attemptedAt = PickupAt.AddMinutes(-15);
        var winnerAt = PickupAt.AddMinutes(-10);
        persistedWinner.ActivateDispatch(winnerAt, Policy.ActivationLeadTime);
        var store = new StubStore(attemptedRide)
        {
            PersistedRideRequest = persistedWinner,
            ThrowConcurrencyOnSave = true,
        };

        var result = await new ActivateScheduledDispatch(
            store, Policy, new CountingTimeProvider(attemptedAt))
            .ExecuteAsync(attemptedRide.Id, CancellationToken.None);

        Assert.Equal(ActivateScheduledDispatchOutcome.AlreadyActivated, result.Outcome);
        Assert.Equal(attemptedAt, attemptedRide.DispatchActivatedAt);
        Assert.Equal(winnerAt, result.RideRequest!.DispatchActivatedAt);
        Assert.NotEqual(attemptedRide.DispatchActivatedAt, result.RideRequest.DispatchActivatedAt);
        Assert.Equal(1, store.SaveCount);
    }

    private static RideRequest CreateRide(RideTiming timing) => RideRequest.Create(
        null, "Hana", "+251911000000", BookingSource.CallCenter, timing,
        9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
        timing == RideTiming.Scheduled ? PickupAt : null, 1200, CreatedAt);

    private sealed class StubStore(RideRequest? rideRequest) : IRideRequestStore
    {
        public RideRequest? RideRequest { get; } = rideRequest;
        public RideRequest? PersistedRideRequest { get; init; } = rideRequest;
        public int SaveCount { get; private set; }
        public bool ThrowConcurrencyOnSave { get; init; }
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(RideRequest?.Id == id ? PersistedRideRequest : null);
        public Task<RideRequest?> FindByIdTrackedAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(RideRequest?.Id == id ? RideRequest : null);
        public Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return ThrowConcurrencyOnSave
                ? Task.FromException(new RideRequestActivationConcurrencyException(new InvalidOperationException()))
                : Task.CompletedTask;
        }
    }

    private sealed class CountingTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public int ReadCount { get; private set; }
        public override DateTimeOffset GetUtcNow() { ReadCount++; return now; }
    }
}
