using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Passengers;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class PassengerConfiguration : IEntityTypeConfiguration<Passenger>
{
    public void Configure(EntityTypeBuilder<Passenger> builder)
    {
        builder.ToTable("passengers");

        builder.HasKey(passenger => passenger.Id);

        builder.Property(passenger => passenger.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(passenger => passenger.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(passenger => passenger.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(32)
            .IsRequired();
    }
}
