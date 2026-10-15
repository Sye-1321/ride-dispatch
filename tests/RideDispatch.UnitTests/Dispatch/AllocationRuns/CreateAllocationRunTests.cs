using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Dispatch.AllocationRuns;

public sealed class CreateAllocationRunTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly DispatchEligibilityPolicy EligibilityPolicy =
        new(50_000, TimeSpan.FromSeconds(60), 0.01m);

    [Fact]
    public async Task Missing_ride_does_not_persist_run()
    {
        var store = new CapturingAllocationRunStore();
        var useCase = CreateUseCase(null, [], store);

        var result = await useCase.ExecuteAsync(
            Guid.CreateVersion7(),
            DispatchRankingPolicy.Nearest,
            CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.RideRequestNotFound, result.Outcome);
        Assert.Null(store.AddedRun);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task Requested_policy_recommends_first_ranked_eligible_driver_and_persists_once()
    {
        var rideRequest = CreateRideRequest();
        var nearerDriverId = Guid.CreateVersion7();
        var longerIdleDriverId = Guid.CreateVersion7();
        var rejectedDriverId = Guid.CreateVersion7();
        var store = new CapturingAllocationRunStore();
        var useCase = CreateUseCase(
            rideRequest,
            [
                Candidate(nearerDriverId, 100, CurrentTime.AddMinutes(-10)),
                Candidate(longerIdleDriverId, 200, CurrentTime.AddHours(-1)),
                RejectedCandidate(rejectedDriverId),
            ],
            store);

        var result = await useCase.ExecuteAsync(
            rideRequest.Id,
            DispatchRankingPolicy.LongestIdle,
            CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.Success, result.Outcome);
        Assert.Equal(longerIdleDriverId, result.AllocationRun!.RecommendedDriverId);
        Assert.Equal(DispatchRankingPolicy.LongestIdle, result.AllocationRun.RankingPolicy);
        Assert.Equal(result.AllocationRun.Id, store.AddedRun!.Id);
        Assert.Equal(1, store.AddCallCount);
        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(3, store.AddedRun.CandidateEvaluations.Count);
        Assert.Equal(1, store.AddedRun.CandidateEvaluations.Single(e => e.DriverId == longerIdleDriverId).Rank);
        Assert.Equal(2, store.AddedRun.CandidateEvaluations.Single(e => e.DriverId == nearerDriverId).Rank);
        var rejected = store.AddedRun.CandidateEvaluations.Single(e => e.DriverId == rejectedDriverId);
        Assert.Null(rejected.Rank);
        Assert.Equal(
            [
                EligibilityRejectionReason.DriverNotApproved,
                EligibilityRejectionReason.DriverNotAvailable,
                EligibilityRejectionReason.AvailabilityTimestampMissing,
            ],
            rejected.Rejections.Select(rejection => rejection.Reason));
    }

    [Fact]
    public async Task No_eligible_drivers_persists_successful_run_without_recommendation()
    {
        var rideRequest = CreateRideRequest();
        var store = new CapturingAllocationRunStore();
        var useCase = CreateUseCase(rideRequest, [], store);

        var result = await useCase.ExecuteAsync(
            rideRequest.Id,
            DispatchRankingPolicy.Nearest,
            CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.Success, result.Outcome);
        Assert.Null(result.AllocationRun!.RecommendedDriverId);
        Assert.Null(store.AddedRun!.RecommendedDriverId);
        Assert.Empty(store.AddedRun.CandidateEvaluations);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public async Task Unactivated_scheduled_ride_is_rejected_without_creating_run()
    {
        var rideRequest = CreateScheduledRideRequest(CurrentTime.AddHours(1));
        var store = new CapturingAllocationRunStore();
        var result = await CreateUseCase(rideRequest, [], store).ExecuteAsync(
            rideRequest.Id, DispatchRankingPolicy.Nearest, CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.ScheduledDispatchNotActivated, result.Outcome);
        Assert.Equal(0, store.AddCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task Activated_scheduled_ride_before_pickup_creates_run()
    {
        var rideRequest = CreateScheduledRideRequest(CurrentTime.AddMinutes(10));
        rideRequest.ActivateDispatch(CurrentTime, TimeSpan.FromMinutes(15));
        var store = new CapturingAllocationRunStore();

        var result = await CreateUseCase(rideRequest, [], store).ExecuteAsync(
            rideRequest.Id, DispatchRankingPolicy.Nearest, CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.Success, result.Outcome);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public async Task Activated_scheduled_ride_at_exact_pickup_creates_run()
    {
        var rideRequest = CreateScheduledRideRequest(CurrentTime);
        rideRequest.ActivateDispatch(CurrentTime.AddMinutes(-1), TimeSpan.FromMinutes(15));
        var store = new CapturingAllocationRunStore();

        var result = await CreateUseCase(rideRequest, [], store).ExecuteAsync(
            rideRequest.Id, DispatchRankingPolicy.Nearest, CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.Success, result.Outcome);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public async Task Activated_scheduled_ride_after_pickup_is_rejected_without_creating_run()
    {
        var pickup = CurrentTime.AddMinutes(-1);
        var rideRequest = RideRequest.Create(
            null, "Hana Gebru", "+251911000000", BookingSource.CallCenter,
            RideTiming.Scheduled, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
            pickup, 1200, CurrentTime.AddMinutes(-20));
        rideRequest.ActivateDispatch(CurrentTime.AddMinutes(-10), TimeSpan.FromMinutes(15));
        var store = new CapturingAllocationRunStore();

        var result = await CreateUseCase(rideRequest, [], store).ExecuteAsync(
            rideRequest.Id, DispatchRankingPolicy.Nearest, CancellationToken.None);

        Assert.Equal(CreateAllocationRunOutcome.ScheduledPickupElapsed, result.Outcome);
        Assert.Equal(0, store.AddCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    private static CreateAllocationRun CreateUseCase(
        RideRequest? rideRequest,
        IReadOnlyList<DispatchCandidateSnapshot> candidates,
        CapturingAllocationRunStore allocationRunStore)
    {
        var rideRequestStore = new StubRideRequestStore(rideRequest);
        var evaluateDispatchCandidates = new EvaluateDispatchCandidates(
            rideRequestStore,
            new StubCandidateStore(candidates),
            EligibilityPolicy,
            new FixedTimeProvider(CurrentTime));
        return new CreateAllocationRun(
            new AllocationRunCreator(
                evaluateDispatchCandidates,
                allocationRunStore,
                new FixedTimeProvider(CurrentTime)),
            rideRequestStore,
            new FixedTimeProvider(CurrentTime));
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

    private static RideRequest CreateScheduledRideRequest(DateTimeOffset pickupAt) =>
        RideRequest.Create(
            null, "Hana Gebru", "+251911000000", BookingSource.CallCenter,
            RideTiming.Scheduled, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
            pickupAt, 1200, CurrentTime.AddMinutes(-1));

    private static DispatchCandidateSnapshot Candidate(
        Guid driverId,
        double distanceMeters,
        DateTimeOffset availableSince) =>
        new(
            driverId,
            DriverApprovalStatus.Approved,
            DriverOperationalStatus.Available,
            availableSince,
            VehicleType.Standard,
            CurrentTime,
            distanceMeters,
            1m,
            false,
            false,
            false);

    private static DispatchCandidateSnapshot RejectedCandidate(Guid driverId) =>
        new(
            driverId,
            DriverApprovalStatus.Pending,
            DriverOperationalStatus.Offline,
            null,
            VehicleType.Standard,
            CurrentTime,
            50,
            1m,
            false,
            false,
            false);

    private sealed class StubRideRequestStore(RideRequest? rideRequest) : IRideRequestStore
    {
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(rideRequest?.Id == id ? rideRequest : null);

        public Task<RideRequest?> FindByIdTrackedAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubCandidateStore(
        IReadOnlyList<DispatchCandidateSnapshot> candidates) : IDispatchCandidateStore
    {
        public Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
            Guid rideRequestId,
            double pickupLatitude,
            double pickupLongitude,
            double radiusMeters,
            DateTimeOffset requestedStartsAt,
            DateTimeOffset requestedEndsAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class CapturingAllocationRunStore : IAllocationRunStore
    {
        public AllocationRun? AddedRun { get; private set; }

        public int AddCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task AddAsync(AllocationRun allocationRun, CancellationToken cancellationToken)
        {
            AddedRun = allocationRun;
            AddCallCount++;
            return Task.CompletedTask;
        }

        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AllocationRun?> FindBySourceOfferIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(
            Guid allocationRunId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
