using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Dispatch.Eligibility;

public sealed class EvaluateDispatchCandidatesTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static readonly DispatchEligibilityPolicy Policy =
        new(50_000, TimeSpan.FromSeconds(60), 0.01m);

    [Fact]
    public async Task Missing_ride_does_not_query_candidates()
    {
        var candidateStore = new StubCandidateStore([]);
        var operation = CreateOperation(null, candidateStore, new FixedTimeProvider(CurrentTime));

        var result = await operation.ExecuteAsync(
            Guid.CreateVersion7(),
            DispatchRankingPolicy.Nearest,
            CancellationToken.None);

        Assert.Equal(EvaluateDispatchCandidatesOutcome.RideRequestNotFound, result.Outcome);
        Assert.False(candidateStore.WasCalled);
    }

    [Fact]
    public async Task All_evaluations_and_multiple_rejection_reasons_are_retained()
    {
        var rideRequest = CreateRideRequest();
        var eligibleId = Guid.CreateVersion7();
        var rejectedId = Guid.CreateVersion7();
        var candidateStore = new StubCandidateStore(
        [
            Candidate(eligibleId, DriverApprovalStatus.Approved, DriverOperationalStatus.Available, CurrentTime),
            Candidate(rejectedId, DriverApprovalStatus.Pending, DriverOperationalStatus.Offline, null),
        ]);
        var operation = CreateOperation(rideRequest, candidateStore, new FixedTimeProvider(CurrentTime));

        var result = await operation.ExecuteAsync(
            rideRequest.Id,
            DispatchRankingPolicy.Nearest,
            CancellationToken.None);

        Assert.Equal(2, result.CandidateEvaluations!.Count);
        var rejected = Assert.Single(result.CandidateEvaluations, evaluation => !evaluation.IsEligible);
        Assert.Contains(EligibilityRejectionReason.DriverNotApproved, rejected.RejectionReasons);
        Assert.Contains(EligibilityRejectionReason.DriverNotAvailable, rejected.RejectionReasons);
        Assert.Contains(EligibilityRejectionReason.AvailabilityTimestampMissing, rejected.RejectionReasons);
        Assert.DoesNotContain(result.RankedEligibleDrivers!, driver => driver.DriverId == rejectedId);
    }

    [Fact]
    public async Task Requested_policy_ranks_eligible_drivers_and_reads_time_once()
    {
        var rideRequest = CreateRideRequest();
        var newerNearerId = Guid.CreateVersion7();
        var longerIdleId = Guid.CreateVersion7();
        var timeProvider = new FixedTimeProvider(CurrentTime);
        var operation = CreateOperation(
            rideRequest,
            new StubCandidateStore(
            [
                Candidate(newerNearerId, DriverApprovalStatus.Approved, DriverOperationalStatus.Available, CurrentTime.AddMinutes(-5), 100),
                Candidate(longerIdleId, DriverApprovalStatus.Approved, DriverOperationalStatus.Available, CurrentTime.AddHours(-1), 200),
            ]),
            timeProvider);

        var result = await operation.ExecuteAsync(
            rideRequest.Id,
            DispatchRankingPolicy.LongestIdle,
            CancellationToken.None);

        Assert.Equal([longerIdleId, newerNearerId], result.RankedEligibleDrivers!.Select(driver => driver.DriverId));
        Assert.Equal(1, timeProvider.CallCount);
    }

    private static EvaluateDispatchCandidates CreateOperation(
        RideRequest? rideRequest,
        StubCandidateStore candidateStore,
        FixedTimeProvider timeProvider) =>
        new(new StubRideRequestStore(rideRequest), candidateStore, Policy, timeProvider);

    private static RideRequest CreateRideRequest() =>
        RideRequest.Create(null, "Hana", "+251911000000", BookingSource.CallCenter,
            RideTiming.Immediate, 9.03, 38.74, 9.01, 38.78, VehicleType.Standard,
            null, 1200, CurrentTime);

    private static DispatchCandidateSnapshot Candidate(
        Guid driverId,
        DriverApprovalStatus approval,
        DriverOperationalStatus status,
        DateTimeOffset? availableSince,
        double distanceMeters = 100) =>
        new(driverId, approval, status, availableSince, VehicleType.Standard,
            CurrentTime, distanceMeters, 1m);

    private sealed class StubRideRequestStore(RideRequest? rideRequest) : IRideRequestStore
    {
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(rideRequest?.Id == id ? rideRequest : null);
        public Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubCandidateStore(IReadOnlyList<DispatchCandidateSnapshot> candidates) : IDispatchCandidateStore
    {
        public bool WasCalled { get; private set; }
        public Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(
            double pickupLatitude, double pickupLongitude, double radiusMeters,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(candidates);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public int CallCount { get; private set; }
        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return currentTime;
        }
    }
}
