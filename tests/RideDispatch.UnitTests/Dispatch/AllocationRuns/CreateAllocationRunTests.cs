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

    private static CreateAllocationRun CreateUseCase(
        RideRequest? rideRequest,
        IReadOnlyList<DispatchCandidateSnapshot> candidates,
        CapturingAllocationRunStore allocationRunStore)
    {
        var evaluateDispatchCandidates = new EvaluateDispatchCandidates(
            new StubRideRequestStore(rideRequest),
            new StubCandidateStore(candidates),
            EligibilityPolicy,
            new FixedTimeProvider(CurrentTime));
        return new CreateAllocationRun(
            evaluateDispatchCandidates,
            allocationRunStore,
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
            1m);

    private static DispatchCandidateSnapshot RejectedCandidate(Guid driverId) =>
        new(
            driverId,
            DriverApprovalStatus.Pending,
            DriverOperationalStatus.Offline,
            null,
            VehicleType.Standard,
            CurrentTime,
            50,
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

    private sealed class StubCandidateStore(
        IReadOnlyList<DispatchCandidateSnapshot> candidates) : IDispatchCandidateStore
    {
        public Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
            double pickupLatitude,
            double pickupLongitude,
            double radiusMeters,
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
