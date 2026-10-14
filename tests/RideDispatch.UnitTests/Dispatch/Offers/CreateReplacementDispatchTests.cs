using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.UnitTests.Dispatch.Offers;

public sealed class CreateReplacementDispatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_source_returns_not_found_without_writes()
    {
        var fixture = Fixture.Missing();
        var result = await fixture.UseCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(CreateReplacementDispatchOutcome.OfferNotFound, result.Outcome);
        Assert.Equal(0, fixture.SourceClock.ReadCount);
        Assert.Equal(0, fixture.Offers.SaveCount);
        Assert.Equal(0, fixture.Runs.AddCount);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Withdrawn)]
    public async Task Non_failure_terminal_state_is_rejected(OfferStatus status)
    {
        var fixture = Fixture.WithSource(status);
        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer!.Id, CancellationToken.None);
        Assert.Equal(CreateReplacementDispatchOutcome.SourceOfferNotFailed, result.Outcome);
        Assert.Equal(0, fixture.SourceClock.ReadCount);
        Assert.Equal(0, fixture.Runs.AddCount);
    }

    [Fact]
    public async Task Pending_before_expiry_is_rejected_without_mutation()
    {
        var fixture = Fixture.WithSource(OfferStatus.Pending, Now.AddSeconds(-1));
        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer!.Id, CancellationToken.None);
        Assert.Equal(CreateReplacementDispatchOutcome.SourceOfferNotFailed, result.Outcome);
        Assert.Equal(OfferStatus.Pending, fixture.SourceOffer.Status);
        Assert.Equal(1, fixture.SourceClock.ReadCount);
        Assert.Equal(0, fixture.Offers.SaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Pending_at_or_after_expiry_is_persisted_then_replaced(int secondsAfterExpiry)
    {
        var fixture = Fixture.WithSource(OfferStatus.Pending, Now.AddSeconds(-30 - secondsAfterExpiry));
        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer!.Id, CancellationToken.None);
        Assert.Equal(CreateReplacementDispatchOutcome.Success, result.Outcome);
        Assert.Equal(OfferStatus.Expired, fixture.SourceOffer.Status);
        Assert.Equal(Now, fixture.SourceOffer.ResolvedAt);
        Assert.Equal(1, fixture.SourceClock.ReadCount);
        Assert.Equal(1, fixture.Offers.SaveCount);
    }

    [Theory]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Expired)]
    public async Task Failed_source_reuses_ride_and_policy_without_reading_clock_for_validation(OfferStatus status)
    {
        var fixture = Fixture.WithSource(status, policy: DispatchRankingPolicy.LongestIdle);
        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer!.Id, CancellationToken.None);
        Assert.Equal(CreateReplacementDispatchOutcome.Success, result.Outcome);
        Assert.Equal(fixture.SourceRun!.RideRequestId, result.AllocationRun!.RideRequestId);
        Assert.Equal(DispatchRankingPolicy.LongestIdle, result.AllocationRun.RankingPolicy);
        Assert.Equal(fixture.SourceOffer.Id, result.AllocationRun.SourceOfferId);
        Assert.Equal(0, fixture.SourceClock.ReadCount);
    }

    [Fact]
    public async Task Existing_run_and_offer_are_reused()
    {
        var fixture = Fixture.WithSource(OfferStatus.Declined, recommendedDriver: true);
        var existingRun = AllocationRun.CreateReplacement(
            fixture.SourceRun!.RideRequestId,
            fixture.SourceRun.RankingPolicy,
            fixture.DriverId,
            fixture.SourceOffer!.Id,
            Now);
        var existingOffer = Offer.Create(existingRun.Id, fixture.DriverId, TimeSpan.FromMinutes(1), Now);
        fixture.Runs.ExistingReplacement = existingRun;
        fixture.Offers.ExistingByRun = existingOffer;

        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer.Id, CancellationToken.None);
        Assert.Equal(existingRun.Id, result.AllocationRun!.Id);
        Assert.Equal(existingOffer.Id, result.Offer!.Id);
        Assert.Equal(0, fixture.Runs.AddCount);
        Assert.Equal(0, fixture.Offers.AddCount);
    }

    [Fact]
    public async Task Existing_recommended_run_recovers_missing_offer()
    {
        var fixture = Fixture.WithSource(OfferStatus.Declined, recommendedDriver: true);
        var existingRun = AllocationRun.CreateReplacement(
            fixture.SourceRun!.RideRequestId, fixture.SourceRun.RankingPolicy, fixture.DriverId,
            fixture.SourceOffer!.Id, Now);
        fixture.Runs.ExistingReplacement = existingRun;

        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer.Id, CancellationToken.None);
        Assert.Equal(existingRun.Id, result.AllocationRun!.Id);
        Assert.NotNull(result.Offer);
        Assert.Equal(1, fixture.Offers.AddCount);
    }

    [Fact]
    public async Task Existing_run_without_recommendation_returns_null_offer()
    {
        var fixture = Fixture.WithSource(OfferStatus.Declined);
        fixture.Runs.ExistingReplacement = AllocationRun.CreateReplacement(
            fixture.SourceRun!.RideRequestId, fixture.SourceRun.RankingPolicy, null,
            fixture.SourceOffer!.Id, Now);

        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer.Id, CancellationToken.None);
        Assert.Equal(CreateReplacementDispatchOutcome.Success, result.Outcome);
        Assert.Null(result.Offer);
        Assert.Equal(0, fixture.Offers.AddCount);
    }

    [Fact]
    public async Task Unique_run_race_reloads_winner()
    {
        var fixture = Fixture.WithSource(OfferStatus.Declined);
        var winner = AllocationRun.CreateReplacement(
            fixture.SourceRun!.RideRequestId, fixture.SourceRun.RankingPolicy, null,
            fixture.SourceOffer!.Id, Now);
        fixture.Runs.WinnerOnSaveRace = winner;

        var result = await fixture.UseCase.ExecuteAsync(fixture.SourceOffer.Id, CancellationToken.None);
        Assert.Equal(winner.Id, result.AllocationRun!.Id);
        Assert.Equal(1, fixture.Runs.AddCount);
    }

    private sealed class Fixture
    {
        private Fixture(
            RideRequest? rideRequest,
            Offer? sourceOffer,
            AllocationRun? sourceRun,
            bool recommendedDriver)
        {
            SourceOffer = sourceOffer;
            SourceRun = sourceRun;
            DriverId = Guid.NewGuid();
            SourceClock = new CountingClock(Now);
            DownstreamClock = new CountingClock(Now);
            Offers = new OfferStoreStub(sourceOffer);
            Runs = new RunStoreStub(sourceRun);
            var evaluator = new EvaluateDispatchCandidates(
                new RideStoreStub(rideRequest),
                new CandidateStoreStub(recommendedDriver ? DriverId : null),
                new DispatchEligibilityPolicy(50_000, TimeSpan.FromMinutes(1), 0.01m),
                DownstreamClock);
            var creator = new AllocationRunCreator(evaluator, Runs, DownstreamClock);
            var createOffer = new CreateOffer(
                Runs,
                Offers,
                new OfferPolicy(TimeSpan.FromMinutes(1)),
                DownstreamClock);
            UseCase = new CreateReplacementDispatch(
                Offers,
                Runs,
                creator,
                createOffer,
                SourceClock);
        }

        public Offer? SourceOffer { get; }
        public AllocationRun? SourceRun { get; }
        public Guid DriverId { get; }
        public CountingClock SourceClock { get; }
        public CountingClock DownstreamClock { get; }
        public OfferStoreStub Offers { get; }
        public RunStoreStub Runs { get; }
        public CreateReplacementDispatch UseCase { get; }

        public static Fixture Missing() => new(null, null, null, false);

        public static Fixture WithSource(
            OfferStatus status,
            DateTimeOffset? createdAt = null,
            DispatchRankingPolicy policy = DispatchRankingPolicy.Nearest,
            bool recommendedDriver = false)
        {
            var rideRequest = CreateRide();
            var run = AllocationRun.Create(
                rideRequest.Id,
                policy,
                Guid.NewGuid(),
                Now.AddMinutes(-2));
            var offer = Offer.Create(run.Id, run.RecommendedDriverId!.Value, TimeSpan.FromSeconds(30), createdAt ?? Now.AddMinutes(-2));
            if (status != OfferStatus.Pending)
            {
                if (status == OfferStatus.Expired) offer.Expire(offer.ExpiresAt);
                else if (status == OfferStatus.Declined) offer.Decline(offer.CreatedAt.AddSeconds(1));
                else if (status == OfferStatus.Accepted) offer.Accept(offer.CreatedAt.AddSeconds(1));
                else offer.Withdraw(offer.CreatedAt.AddSeconds(1));
            }
            return new(rideRequest, offer, run, recommendedDriver);
        }

        private static RideRequest CreateRide() =>
            RideRequest.Create(null, "Passenger", "+251911000000", BookingSource.CallCenter,
                RideTiming.Immediate, 9.03, 38.74, 9.01, 38.78,
                RideDispatch.Domain.Vehicles.VehicleType.Standard, null, 1200, Now);
    }

    private sealed class CountingClock(DateTimeOffset now) : TimeProvider
    {
        public int ReadCount { get; private set; }
        public override DateTimeOffset GetUtcNow() { ReadCount++; return now; }
    }

    private sealed class RunStoreStub(AllocationRun? source) : IAllocationRunStore
    {
        public AllocationRun? ExistingReplacement { get; set; }
        public AllocationRun? WinnerOnSaveRace { get; set; }
        public int AddCount { get; private set; }
        public Task AddAsync(AllocationRun run, CancellationToken ct) { ExistingReplacement = run; AddCount++; return Task.CompletedTask; }
        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(source?.Id == id ? source : ExistingReplacement?.Id == id ? ExistingReplacement : null);
        public Task<AllocationRun?> FindBySourceOfferIdAsync(Guid id, CancellationToken ct) => Task.FromResult(ExistingReplacement?.SourceOfferId == id ? ExistingReplacement : null);
        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct)
        {
            if (WinnerOnSaveRace is not null) { ExistingReplacement = WinnerOnSaveRace; WinnerOnSaveRace = null; throw new ReplacementAllocationRunAlreadyExistsException(new Exception()); }
            return Task.CompletedTask;
        }
    }

    private sealed class OfferStoreStub(Offer? source) : IOfferStore
    {
        public Offer? ExistingByRun { get; set; }
        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }
        public Task AddAsync(Offer offer, CancellationToken ct) { ExistingByRun = offer; AddCount++; return Task.CompletedTask; }
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(source?.Id == id ? source : null);
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => FindByIdAsync(id, ct);
        public Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct) => Task.FromResult(ExistingByRun?.AllocationRunId == id ? ExistingByRun : null);
        public Task SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class RideStoreStub(RideRequest? ride) : IRideRequestStore
    {
        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(ride?.Id == id ? ride : null);
        public Task AddAsync(RideRequest value, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CandidateStoreStub(Guid? driverId) : IDispatchCandidateStore
    {
        public Task<IReadOnlyList<DispatchCandidateSnapshot>> FindWithinRadiusAsync(Guid rideId, double lat, double lon, double radius, DateTimeOffset starts, DateTimeOffset ends, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DispatchCandidateSnapshot>>(driverId is null ? [] :
            [new(driverId.Value, RideDispatch.Domain.Drivers.DriverApprovalStatus.Approved,
                RideDispatch.Domain.Drivers.DriverOperationalStatus.Available, Now.AddHours(-1),
                RideDispatch.Domain.Vehicles.VehicleType.Standard, Now, 10, 1m, false, false, false)]);
    }
}
