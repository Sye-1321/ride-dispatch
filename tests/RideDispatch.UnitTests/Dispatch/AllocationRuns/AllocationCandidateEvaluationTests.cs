using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.AllocationRuns;

public sealed class AllocationCandidateEvaluationTests
{
    private static readonly Guid RunId = Guid.CreateVersion7();
    private static readonly Guid DriverId = Guid.CreateVersion7();
    private static readonly DateTimeOffset RecordedAt = DateTimeOffset.UtcNow;

    [Fact]
    public void Eligible_evaluation_accepts_positive_rank_without_rejections()
    {
        var evaluation = Create(true, 1, 100, []);

        Assert.Equal(1, evaluation.Rank);
        Assert.Empty(evaluation.Rejections);
    }

    [Fact]
    public void Rejected_evaluation_accepts_reasons_and_no_rank()
    {
        var evaluation = Create(false, null, 100,
            [EligibilityRejectionReason.DriverNotApproved, EligibilityRejectionReason.VehicleMissing]);

        Assert.Null(evaluation.Rank);
        Assert.Equal(2, evaluation.Rejections.Count);
    }

    [Fact]
    public void Eligibility_rank_and_rejection_invariants_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => Create(true, 1, 100, [EligibilityRejectionReason.LocationStale]));
        Assert.Throws<ArgumentException>(() => Create(false, 1, 100, [EligibilityRejectionReason.LocationStale]));
        Assert.Throws<ArgumentException>(() => Create(false, null, 100, []));
        Assert.Throws<ArgumentException>(() => Create(true, 0, 100, []));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void Invalid_distance_is_rejected(double distanceMeters) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(true, 1, distanceMeters, []));

    [Fact]
    public void Empty_structural_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => AllocationCandidateEvaluation.Create(
            Guid.Empty, DriverId, true, 1, 100, RecordedAt, RecordedAt, []));
        Assert.Throws<ArgumentException>(() => AllocationCandidateEvaluation.Create(
            RunId, Guid.Empty, true, 1, 100, RecordedAt, RecordedAt, []));
    }

    [Fact]
    public void Eligible_evaluation_requires_availability_timestamp()
    {
        Assert.Throws<ArgumentException>(() =>
            AllocationCandidateEvaluation.Create(
                RunId,
                DriverId,
                true,
                1,
                100,
                null,
                RecordedAt,
                []));
    }

    private static AllocationCandidateEvaluation Create(
        bool isEligible,
        int? rank,
        double distanceMeters,
        IReadOnlyCollection<EligibilityRejectionReason> reasons) =>
        AllocationCandidateEvaluation.Create(
            RunId, DriverId, isEligible, rank, distanceMeters, RecordedAt, RecordedAt, reasons);
}
