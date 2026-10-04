using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Drivers;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(driver => driver.Id);

        builder.Property(driver => driver.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(driver => driver.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(driver => driver.ApprovalStatus)
            .HasColumnName("approval_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(driver => driver.OperationalStatus)
            .HasColumnName("operational_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(driver => driver.AvailableSince)
            .HasColumnName("available_since")
            .HasColumnType("timestamp with time zone");
    }
}
