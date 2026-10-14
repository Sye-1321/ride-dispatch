using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments", table =>
        {
            table.HasCheckConstraint("ck_assignments_ends_after_starts", "ends_at > starts_at");
            table.HasCheckConstraint("ck_assignments_starts_not_before_created", "starts_at >= created_at");
            table.HasCheckConstraint("ck_assignments_status_resolved", "(status = 'Active' AND resolved_at IS NULL) OR (status IN ('Completed', 'Cancelled') AND resolved_at IS NOT NULL)");
            table.HasCheckConstraint("ck_assignments_resolved_not_before_created", "resolved_at IS NULL OR resolved_at >= created_at");
            table.HasCheckConstraint("ck_assignments_completed_not_before_start", "status <> 'Completed' OR resolved_at >= starts_at");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OfferId).HasColumnName("offer_id").IsRequired();
        builder.Property(x => x.RideRequestId).HasColumnName("ride_request_id").IsRequired();
        builder.Property(x => x.DriverId).HasColumnName("driver_id").IsRequired();
        builder.Property(x => x.StartsAt).HasColumnName("starts_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.EndsAt).HasColumnName("ends_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.OfferId).IsUnique().HasDatabaseName("ux_assignments_offer_id");
        builder.HasIndex(x => x.RideRequestId).IsUnique().HasFilter("status <> 'Cancelled'").HasDatabaseName("ux_assignments_ride_request_id");
        builder.HasOne<Offer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RideRequest>().WithMany().HasForeignKey(x => x.RideRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}
