using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Drivers;
using RideDispatch.Infrastructure.Persistence.Models;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class DriverFinancialStandingConfiguration
    : IEntityTypeConfiguration<DriverFinancialStandingRecord>
{
    public void Configure(EntityTypeBuilder<DriverFinancialStandingRecord> builder)
    {
        builder.ToTable(
            "driver_financial_standings",
            table => table.HasCheckConstraint(
                "ck_driver_financial_standings_commission_balance_nonnegative",
                "commission_balance >= 0"));

        builder.HasKey(standing => standing.DriverId);

        builder.Property(standing => standing.DriverId)
            .HasColumnName("driver_id")
            .ValueGeneratedNever();

        builder.Property(standing => standing.CommissionBalance)
            .HasColumnName("commission_balance")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(standing => standing.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Driver>()
            .WithOne()
            .HasForeignKey<DriverFinancialStandingRecord>(standing => standing.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
