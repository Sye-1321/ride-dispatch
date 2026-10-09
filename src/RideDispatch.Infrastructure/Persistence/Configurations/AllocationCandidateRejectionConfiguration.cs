using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class AllocationCandidateRejectionConfiguration :
    IEntityTypeConfiguration<AllocationCandidateRejection>
{
    public void Configure(EntityTypeBuilder<AllocationCandidateRejection> builder)
    {
        builder.ToTable("allocation_candidate_rejections");

        builder.HasKey(rejection => new
        {
            rejection.AllocationRunId,
            rejection.DriverId,
            rejection.Reason,
        });

        builder.Property(rejection => rejection.AllocationRunId)
            .HasColumnName("allocation_run_id");
        builder.Property(rejection => rejection.DriverId)
            .HasColumnName("driver_id");
        builder.Property(rejection => rejection.Reason)
            .HasColumnName("reason")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne<AllocationCandidateEvaluation>()
            .WithMany(evaluation => evaluation.Rejections)
            .HasForeignKey(rejection => new { rejection.AllocationRunId, rejection.DriverId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
