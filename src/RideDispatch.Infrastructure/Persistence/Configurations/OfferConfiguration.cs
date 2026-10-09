using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers", table =>
        {
            table.HasCheckConstraint("ck_offers_expires_after_created", "expires_at > created_at");
            table.HasCheckConstraint(
                "ck_offers_resolved_not_before_created",
                "resolved_at IS NULL OR resolved_at >= created_at");
            table.HasCheckConstraint("ck_offers_status_resolved", "(status = 'Pending' AND resolved_at IS NULL) OR (status <> 'Pending' AND resolved_at IS NOT NULL)");
        });
        builder.HasKey(offer => offer.Id);
        builder.Property(offer => offer.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(offer => offer.AllocationRunId).HasColumnName("allocation_run_id").IsRequired();
        builder.Property(offer => offer.DriverId).HasColumnName("driver_id").IsRequired();
        builder.Property(offer => offer.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(offer => offer.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(offer => offer.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(offer => offer.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(offer => offer.AllocationRunId).IsUnique().HasDatabaseName("ux_offers_allocation_run_id");
        builder.HasOne<AllocationRun>().WithMany().HasForeignKey(offer => offer.AllocationRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Driver>().WithMany().HasForeignKey(offer => offer.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}
