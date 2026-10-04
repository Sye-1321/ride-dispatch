using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Drivers;
using RideDispatch.Infrastructure.Persistence.Models;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class DriverLocationConfiguration : IEntityTypeConfiguration<DriverLocationRecord>
{
    public void Configure(EntityTypeBuilder<DriverLocationRecord> builder)
    {
        builder.ToTable("driver_locations");

        builder.HasKey(location => location.DriverId);

        builder.Property(location => location.DriverId)
            .HasColumnName("driver_id")
            .ValueGeneratedNever();

        builder.Property(location => location.Position)
            .HasColumnName("position")
            .HasColumnType("geography (point, 4326)")
            .IsRequired();

        builder.Property(location => location.RecordedAt)
            .HasColumnName("recorded_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(location => location.Position)
            .HasMethod("gist");

        builder.HasOne<Driver>()
            .WithOne()
            .HasForeignKey<DriverLocationRecord>(location => location.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
