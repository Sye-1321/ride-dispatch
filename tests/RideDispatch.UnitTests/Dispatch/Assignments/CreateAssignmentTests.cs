using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Assignments;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Dispatch.Assignments;

public sealed class CreateAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_offer_reads_no_clock_or_persistence()
    {
        var clock = new CountingClock(Now);
        var store = new StubAssignmentStore();
        var operation = CreateOperation(null, null, null, store, clock);

        var result = await operation.ExecuteAsync(Guid.NewGuid(), default);

        Assert.Equal(CreateAssignmentOutcome.OfferNotFound, result.Outcome);
        Assert.Equal(0, clock.Count);
        Assert.Equal(0, store.AddCount);
    }

    [Theory]
    [InlineData(OfferStatus.Pending)]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Expired)]
    [InlineData(OfferStatus.Withdrawn)]
    public async Task Offer_must_be_accepted(OfferStatus status)
    {
        var offer = OfferWithStatus(status);
        var result = await CreateOperation(offer, null, null, new(), new(Now)).ExecuteAsync(offer.Id, default);
        Assert.Equal(CreateAssignmentOutcome.OfferNotAccepted, result.Outcome);
    }

    [Fact]
    public async Task Accepted_offer_creates_immediate_assignment_once_with_offer_driver_and_ride()
    {
        var driverId = Guid.NewGuid();
        var ride = Ride(RideTiming.Immediate);
        var run = AllocationRun.Create(ride.Id, DispatchRankingPolicy.Nearest, driverId, Now.AddMinutes(-1));
        var offer = Offer.Create(run.Id, driverId, TimeSpan.FromHours(1), Now.AddMinutes(-1));
        offer.Accept(Now.AddSeconds(-1));
        var store = new StubAssignmentStore();
        var clock = new CountingClock(Now);

        var result = await CreateOperation(offer, run, ride, store, clock).ExecuteAsync(offer.Id, default);

        Assert.Equal(CreateAssignmentOutcome.Success, result.Outcome);
        Assert.Equal(driverId, result.Assignment!.DriverId);
        Assert.Equal(ride.Id, result.Assignment.RideRequestId);
        Assert.Equal(Now, result.Assignment.StartsAt);
        Assert.Equal(Now.AddSeconds(1200), result.Assignment.EndsAt);
        Assert.Equal(1, clock.Count);
        Assert.Equal(1, store.AddCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Elapsed_scheduled_pickup_is_rejected_but_exact_now_succeeds()
    {
        var elapsed = await ExecuteScheduled(Now.AddTicks(-1));
        var exact = await ExecuteScheduled(Now);
        Assert.Equal(CreateAssignmentOutcome.ScheduledPickupElapsed, elapsed.Outcome);
        Assert.Equal(CreateAssignmentOutcome.Success, exact.Outcome);
    }

    [Theory]
    [InlineData(true, false, CreateAssignmentOutcome.AssignmentAlreadyExists)]
    [InlineData(false, true, CreateAssignmentOutcome.DriverScheduleConflict)]
    public async Task Prechecks_return_business_outcomes(bool duplicate, bool overlap, CreateAssignmentOutcome expected)
    {
        var (offer, run, ride) = Graph();
        var store = new StubAssignmentStore { DuplicateOffer = duplicate, Overlap = overlap };
        var result = await CreateOperation(offer, run, ride, store, new(Now)).ExecuteAsync(offer.Id, default);
        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task Existing_assignment_for_ride_returns_duplicate_without_persistence()
    {
        var (offer, run, ride) = Graph();
        var store = new StubAssignmentStore { DuplicateRide = true };

        var result = await CreateOperation(offer, run, ride, store, new(Now)).ExecuteAsync(offer.Id, default);

        Assert.Equal(CreateAssignmentOutcome.AssignmentAlreadyExists, result.Outcome);
        Assert.Equal(0, store.AddCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(true, CreateAssignmentOutcome.AssignmentAlreadyExists)]
    [InlineData(false, CreateAssignmentOutcome.DriverScheduleConflict)]
    public async Task Database_race_exceptions_are_mapped(bool duplicate, CreateAssignmentOutcome expected)
    {
        var (offer, run, ride) = Graph();
        var store = new StubAssignmentStore
        {
            SaveException = duplicate
            ? new AssignmentAlreadyExistsException(new Exception())
            : new AssignmentScheduleConflictException(new Exception())
        };
        var result = await CreateOperation(offer, run, ride, store, new(Now)).ExecuteAsync(offer.Id, default);
        Assert.Equal(expected, result.Outcome);
    }

    private static async Task<CreateAssignmentResult> ExecuteScheduled(DateTimeOffset pickup)
    {
        var ride = Ride(RideTiming.Scheduled, pickup);
        var run = AllocationRun.Create(ride.Id, DispatchRankingPolicy.Nearest, Guid.NewGuid(), Now.AddMinutes(-2));
        var offer = Offer.Create(run.Id, run.RecommendedDriverId!.Value, TimeSpan.FromHours(1), Now.AddMinutes(-1));
        offer.Accept(Now.AddSeconds(-1));
        return await CreateOperation(offer, run, ride, new(), new(Now)).ExecuteAsync(offer.Id, default);
    }

    private static (Offer Offer, AllocationRun Run, RideRequest Ride) Graph()
    {
        var ride = Ride(RideTiming.Immediate);
        var run = AllocationRun.Create(ride.Id, DispatchRankingPolicy.Nearest, Guid.NewGuid(), Now.AddMinutes(-2));
        var offer = Offer.Create(run.Id, run.RecommendedDriverId!.Value, TimeSpan.FromHours(1), Now.AddMinutes(-1));
        offer.Accept(Now.AddSeconds(-1));
        return (offer, run, ride);
    }

    private static RideRequest Ride(RideTiming timing, DateTimeOffset? pickup = null) =>
        RideRequest.Create(null, "Passenger", "+251900000000", BookingSource.CallCenter, timing,
            9, 38, 9.1, 38.1, VehicleType.Standard, pickup, 1200, Now.AddHours(-1));

    private static Offer OfferWithStatus(OfferStatus status)
    {
        var offer = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10), Now.AddMinutes(-2));
        if (status == OfferStatus.Pending) return offer;
        if (status == OfferStatus.Declined) offer.Decline(Now.AddMinutes(-1));
        if (status == OfferStatus.Withdrawn) offer.Withdraw(Now.AddMinutes(-1));
        if (status == OfferStatus.Expired) offer.Expire(Now.AddMinutes(8));
        return offer;
    }

    private static CreateAssignment CreateOperation(Offer? offer, AllocationRun? run, RideRequest? ride,
        StubAssignmentStore store, CountingClock clock) => new(new OfferStoreStub(offer),
        new RunStoreStub(run), new RideStoreStub(ride), store, clock);

    private sealed class CountingClock(DateTimeOffset now) : TimeProvider
    { public int Count { get; private set; } public override DateTimeOffset GetUtcNow() { Count++; return now; } }

    private sealed class OfferStoreStub(Offer? value) : IOfferStore
    {
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => Task.FromResult(value?.Id == id ? value : null);
        public Task AddAsync(Offer x, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class RunStoreStub(AllocationRun? value) : IAllocationRunStore
    {
        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(value?.Id == id ? value : null);
        public Task AddAsync(AllocationRun x, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class RideStoreStub(RideRequest? value) : IRideRequestStore
    {
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(value?.Id == id ? value : null);
        public Task AddAsync(RideRequest x, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class StubAssignmentStore : IAssignmentStore
    {
        public bool DuplicateOffer { get; init; }
        public bool DuplicateRide { get; init; }
        public bool Overlap { get; init; }
        public Exception? SaveException { get; init; }
        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }
        public Task AddAsync(Assignment x, CancellationToken ct) { AddCount++; return Task.CompletedTask; }
        public Task<Assignment?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => Task.FromResult<Assignment?>(null);
        public Task<Assignment?> FindByOfferIdAsync(Guid id, CancellationToken ct) => Task.FromResult(DuplicateOffer ? Fake() : null);
        public Task<Assignment?> FindByRideRequestIdAsync(Guid id, CancellationToken ct) => Task.FromResult(DuplicateRide ? Fake() : null);
        public Task<bool> HasDriverOverlapAsync(Guid id, DateTimeOffset s, DateTimeOffset e, CancellationToken ct) => Task.FromResult(Overlap);
        public Task SaveChangesAsync(CancellationToken ct) { SaveCount++; return SaveException is null ? Task.CompletedTask : Task.FromException(SaveException); }
        private static Assignment Fake() => Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, TimeSpan.FromMinutes(1), Now);
    }
}
