using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Drivers;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(vehicle => vehicle.Id);

        builder.Property(vehicle => vehicle.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(vehicle => vehicle.DriverId)
            .HasColumnName("driver_id")
            .IsRequired();

        builder.Property(vehicle => vehicle.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(vehicle => vehicle.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(vehicle => vehicle.DriverId)
            .IsUnique();

        builder.HasOne<Driver>()
            .WithOne()
            .HasForeignKey<Vehicle>(vehicle => vehicle.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
