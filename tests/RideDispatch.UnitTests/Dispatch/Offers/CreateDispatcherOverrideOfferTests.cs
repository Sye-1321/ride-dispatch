using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Offers;

public sealed class CreateDispatcherOverrideOfferTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_run_returns_not_found_without_clock()
    {
        var fixture = Fixture.Missing();
        var result = await fixture.UseCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), "Reason", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.AllocationRunNotFound, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
        Assert.Equal(0, fixture.Offers.AddCount);
    }

    [Fact]
    public async Task Driver_absent_from_evidence_is_rejected_without_clock()
    {
        var fixture = Fixture.Valid();
        var result = await fixture.UseCase.ExecuteAsync(fixture.Run!.Id, Guid.NewGuid(), "Reason", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.DriverNotEvaluated, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Fact]
    public async Task Rejected_driver_is_ineligible_without_clock()
    {
        var fixture = Fixture.Valid(includeRejected: true);
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run!.Id, fixture.RejectedDriverId, "Reason", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.DriverNotEligible, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Fact]
    public async Task Recommended_driver_cannot_be_labelled_override()
    {
        var fixture = Fixture.Valid();
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run!.Id, fixture.RecommendedDriverId, "Reason", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.DriverAlreadyRecommended, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Invalid_reason_is_rejected_without_clock(string? reason)
    {
        var fixture = Fixture.Valid();
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run!.Id, fixture.AlternateDriverId, reason, default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.InvalidReason, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Fact]
    public async Task Overlong_reason_is_rejected_without_clock()
    {
        var fixture = Fixture.Valid();
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run!.Id, fixture.AlternateDriverId, new string('x', 501), default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.InvalidReason, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
    }

    [Fact]
    public async Task Existing_offer_is_rejected_without_clock_or_second_add()
    {
        var fixture = Fixture.Valid(existingOffer: true);
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run!.Id, fixture.AlternateDriverId, "Reason", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.OfferAlreadyExists, result.Outcome);
        Assert.Equal(0, fixture.Clock.ReadCount);
        Assert.Equal(0, fixture.Offers.AddCount);
    }

    [Fact]
    public async Task Eligible_rank_two_driver_creates_audited_offer_without_mutating_run()
    {
        var fixture = Fixture.Valid();
        var originalRecommendation = fixture.Run!.RecommendedDriverId;
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run.Id, fixture.AlternateDriverId, "  Dispatch preference  ", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.Success, result.Outcome);
        Assert.Equal(fixture.AlternateDriverId, result.Offer!.DriverId);
        Assert.Equal(OfferSelectionMode.DispatcherOverride, result.Offer.SelectionMode);
        Assert.Equal("Dispatch preference", result.Offer.OverrideReason);
        Assert.Equal(originalRecommendation, fixture.Run.RecommendedDriverId);
        Assert.Equal(1, fixture.Clock.ReadCount);
        Assert.Equal(1, fixture.Offers.AddCount);
        Assert.Equal(1, fixture.Offers.SaveCount);
    }

    [Fact]
    public async Task Unique_constraint_race_returns_existing_outcome()
    {
        var fixture = Fixture.Valid(uniqueRace: true);
        var result = await fixture.UseCase.ExecuteAsync(
            fixture.Run!.Id, fixture.AlternateDriverId, "Reason", default);
        Assert.Equal(CreateDispatcherOverrideOfferOutcome.OfferAlreadyExists, result.Outcome);
        Assert.Equal(1, fixture.Clock.ReadCount);
        Assert.Equal(1, fixture.Offers.AddCount);
    }

    private sealed class Fixture
    {
        private Fixture(AllocationRun? run, IReadOnlyList<AllocationCandidateEvaluation>? evaluations,
            bool existingOffer, bool uniqueRace, Guid recommendedDriverId,
            Guid alternateDriverId, Guid rejectedDriverId)
        {
            Run = run;
            RecommendedDriverId = recommendedDriverId;
            AlternateDriverId = alternateDriverId;
            RejectedDriverId = rejectedDriverId;
            Clock = new CountingClock(Now);
            Offers = new OfferStoreStub(run, existingOffer, uniqueRace);
            UseCase = new CreateDispatcherOverrideOffer(
                new RunStoreStub(run, evaluations), Offers,
                new OfferPolicy(TimeSpan.FromSeconds(37)), Clock);
        }

        public AllocationRun? Run { get; }
        public Guid RecommendedDriverId { get; }
        public Guid AlternateDriverId { get; }
        public Guid RejectedDriverId { get; }
        public CountingClock Clock { get; }
        public OfferStoreStub Offers { get; }
        public CreateDispatcherOverrideOffer UseCase { get; }

        public static Fixture Missing() => new(null, null, false, false,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        public static Fixture Valid(bool includeRejected = false, bool existingOffer = false,
            bool uniqueRace = false)
        {
            var recommended = Guid.NewGuid();
            var alternate = Guid.NewGuid();
            var rejected = Guid.NewGuid();
            var run = AllocationRun.Create(Guid.NewGuid(), DispatchRankingPolicy.Nearest,
                recommended, Now);
            var evaluations = new List<AllocationCandidateEvaluation>
            {
                Eligible(run.Id, recommended, 1),
                Eligible(run.Id, alternate, 2),
            };
            if (includeRejected)
                evaluations.Add(Rejected(run.Id, rejected));
            return new(run, evaluations, existingOffer, uniqueRace,
                recommended, alternate, rejected);
        }

        private static AllocationCandidateEvaluation Eligible(Guid runId, Guid driverId, int rank) =>
            AllocationCandidateEvaluation.Create(runId, driverId, true, rank, rank * 10,
                Now.AddMinutes(-10), Now, []);

        private static AllocationCandidateEvaluation Rejected(Guid runId, Guid driverId) =>
            AllocationCandidateEvaluation.Create(runId, driverId, false, null, 30,
                null, Now, [EligibilityRejectionReason.VehicleTypeMismatch]);
    }

    private sealed class CountingClock(DateTimeOffset now) : TimeProvider
    {
        public int ReadCount { get; private set; }
        public override DateTimeOffset GetUtcNow() { ReadCount++; return now; }
    }

    private sealed class RunStoreStub(AllocationRun? run,
        IReadOnlyList<AllocationCandidateEvaluation>? evaluations) : IAllocationRunStore
    {
        public Task<AllocationRun?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(run?.Id == id ? run : null);
        public Task<IReadOnlyList<AllocationCandidateEvaluation>?> FindCandidateEvaluationsAsync(
            Guid id, CancellationToken ct) => Task.FromResult(run?.Id == id ? evaluations : null);
        public Task AddAsync(AllocationRun value, CancellationToken ct) => throw new NotSupportedException();
        public Task<AllocationRun?> FindBySourceOfferIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class OfferStoreStub(
        AllocationRun? run, bool existingOffer, bool uniqueRace) : IOfferStore
    {
        public int AddCount { get; private set; }
        public int SaveCount { get; private set; }
        public Task AddAsync(Offer offer, CancellationToken ct) { AddCount++; return Task.CompletedTask; }
        public Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(existingOffer && run is not null
                ? Offer.Create(run.Id, run.RecommendedDriverId!.Value, TimeSpan.FromMinutes(1), Now)
                : null);
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return uniqueRace
                ? Task.FromException(new OfferAlreadyExistsException(new Exception()))
                : Task.CompletedTask;
        }
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
