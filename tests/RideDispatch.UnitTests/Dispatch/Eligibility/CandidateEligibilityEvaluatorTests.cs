using RideDispatch.Application.Dispatch.Eligibility;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Dispatch.Eligibility;

public sealed class CandidateEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly DispatchEligibilityPolicy Policy =
        new(50_000, TimeSpan.FromSeconds(60), 0.01m);

    [Fact]
    public void Fully_valid_candidate_is_eligible()
    {
        var evaluation = Evaluate(Candidate());

        Assert.True(evaluation.IsEligible);
        Assert.Empty(evaluation.RejectionReasons);
    }

    [Fact]
    public void Not_approved_is_rejected()
    {
        AssertRejected(
            Candidate(approvalStatus: DriverApprovalStatus.Pending),
            EligibilityRejectionReason.DriverNotApproved);
    }

    [Theory]
    [InlineData(DriverOperationalStatus.Offline)]
    [InlineData(DriverOperationalStatus.Unavailable)]
    public void Nonavailable_status_is_rejected(DriverOperationalStatus status)
    {
        AssertRejected(
            Candidate(operationalStatus: status),
            EligibilityRejectionReason.DriverNotAvailable);
    }

    [Fact]
    public void Available_status_without_timestamp_fails_closed()
    {
        AssertRejected(
            Candidate() with { AvailableSince = null },
            EligibilityRejectionReason.AvailabilityTimestampMissing);
    }

    [Fact]
    public void Missing_vehicle_is_rejected_without_mismatch_reason()
    {
        var evaluation = Evaluate(Candidate(vehicleType: null));

        Assert.Contains(EligibilityRejectionReason.VehicleMissing, evaluation.RejectionReasons);
        Assert.DoesNotContain(
            EligibilityRejectionReason.VehicleTypeMismatch,
            evaluation.RejectionReasons);
    }

    [Fact]
    public void Vehicle_type_mismatch_is_rejected()
    {
        AssertRejected(
            Candidate(vehicleType: VehicleType.Xl),
            EligibilityRejectionReason.VehicleTypeMismatch);
    }

    [Fact]
    public void Stale_location_is_rejected()
    {
        AssertRejected(
            Candidate(locationRecordedAt: CurrentTime.AddSeconds(-61)),
            EligibilityRejectionReason.LocationStale);
    }

    [Fact]
    public void Freshness_boundary_is_accepted()
    {
        var evaluation = Evaluate(Candidate(locationRecordedAt: CurrentTime.AddSeconds(-60)));

        Assert.True(evaluation.IsEligible);
    }

    [Fact]
    public void Missing_financial_standing_is_rejected_without_low_balance_reason()
    {
        var evaluation = Evaluate(Candidate(commissionBalance: null));

        Assert.Contains(
            EligibilityRejectionReason.FinancialStandingMissing,
            evaluation.RejectionReasons);
        Assert.DoesNotContain(
            EligibilityRejectionReason.CommissionBalanceBelowMinimum,
            evaluation.RejectionReasons);
    }

    [Fact]
    public void Balance_below_minimum_is_rejected()
    {
        AssertRejected(
            Candidate(commissionBalance: 0m),
            EligibilityRejectionReason.CommissionBalanceBelowMinimum);
    }

    [Fact]
    public void Balance_equal_to_minimum_is_accepted()
    {
        Assert.True(Evaluate(Candidate(commissionBalance: 0.01m)).IsEligible);
    }

    [Fact]
    public void Multiple_simultaneous_failures_are_accumulated()
    {
        var evaluation = Evaluate(Candidate(
            approvalStatus: DriverApprovalStatus.Pending,
            operationalStatus: DriverOperationalStatus.Offline,
            vehicleType: null,
            locationRecordedAt: CurrentTime.AddMinutes(-2),
            commissionBalance: null) with
        { AvailableSince = null });

        Assert.Equal(
            [
                EligibilityRejectionReason.DriverNotApproved,
                EligibilityRejectionReason.DriverNotAvailable,
                EligibilityRejectionReason.AvailabilityTimestampMissing,
                EligibilityRejectionReason.VehicleMissing,
                EligibilityRejectionReason.LocationStale,
                EligibilityRejectionReason.FinancialStandingMissing,
            ],
            evaluation.RejectionReasons);
    }

    private static CandidateEligibilityEvaluation Evaluate(DispatchCandidateSnapshot candidate) =>
        CandidateEligibilityEvaluator.Evaluate(
            candidate,
            VehicleType.Standard,
            CurrentTime,
            Policy);

    private static void AssertRejected(
        DispatchCandidateSnapshot candidate,
        EligibilityRejectionReason expectedReason)
    {
        var evaluation = Evaluate(candidate);
        Assert.False(evaluation.IsEligible);
        Assert.Contains(expectedReason, evaluation.RejectionReasons);
    }

    private static DispatchCandidateSnapshot Candidate(
        DriverApprovalStatus approvalStatus = DriverApprovalStatus.Approved,
        DriverOperationalStatus operationalStatus = DriverOperationalStatus.Available,
        VehicleType? vehicleType = VehicleType.Standard,
        DateTimeOffset? locationRecordedAt = null,
        decimal? commissionBalance = 1m) =>
        new(
            Guid.CreateVersion7(),
            approvalStatus,
            operationalStatus,
            CurrentTime.AddHours(-1),
            vehicleType,
            locationRecordedAt ?? CurrentTime,
            100,
            commissionBalance);
}
