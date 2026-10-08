using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class AllocationRunConfiguration : IEntityTypeConfiguration<AllocationRun>
{
    public void Configure(EntityTypeBuilder<AllocationRun> builder)
    {
        builder.ToTable("allocation_runs");

        builder.HasKey(allocationRun => allocationRun.Id);

        builder.Property(allocationRun => allocationRun.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(allocationRun => allocationRun.RideRequestId)
            .HasColumnName("ride_request_id")
            .IsRequired();

        builder.Property(allocationRun => allocationRun.RankingPolicy)
            .HasColumnName("ranking_policy")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(allocationRun => allocationRun.RecommendedDriverId)
            .HasColumnName("recommended_driver_id");

        builder.Property(allocationRun => allocationRun.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<RideRequest>()
            .WithMany()
            .HasForeignKey(allocationRun => allocationRun.RideRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(allocationRun => allocationRun.RecommendedDriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
