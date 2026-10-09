using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class AllocationCandidateEvaluationConfiguration :
    IEntityTypeConfiguration<AllocationCandidateEvaluation>
{
    public void Configure(EntityTypeBuilder<AllocationCandidateEvaluation> builder)
    {
        builder.ToTable(
            "allocation_candidate_evaluations",
            table => table.HasCheckConstraint(
                "ck_allocation_candidate_evaluations_rank",
                "(is_eligible AND rank IS NOT NULL AND rank > 0) OR " +
                "(NOT is_eligible AND rank IS NULL)"));

        builder.HasKey(evaluation => new { evaluation.AllocationRunId, evaluation.DriverId });

        builder.Property(evaluation => evaluation.AllocationRunId)
            .HasColumnName("allocation_run_id");
        builder.Property(evaluation => evaluation.DriverId)
            .HasColumnName("driver_id");
        builder.Property(evaluation => evaluation.IsEligible)
            .HasColumnName("is_eligible")
            .IsRequired();
        builder.Property(evaluation => evaluation.Rank)
            .HasColumnName("rank");
        builder.Property(evaluation => evaluation.DistanceMeters)
            .HasColumnName("distance_meters")
            .HasColumnType("double precision")
            .IsRequired();
        builder.Property(evaluation => evaluation.AvailableSince)
            .HasColumnName("available_since")
            .HasColumnType("timestamp with time zone");
        builder.Property(evaluation => evaluation.LocationRecordedAt)
            .HasColumnName("location_recorded_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<AllocationRun>()
            .WithMany(run => run.CandidateEvaluations)
            .HasForeignKey(evaluation => evaluation.AllocationRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(evaluation => evaluation.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(evaluation => evaluation.Rejections)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
