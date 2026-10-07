using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideDispatch.Domain.Passengers;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Infrastructure.Persistence.Configurations;

public sealed class RideRequestConfiguration : IEntityTypeConfiguration<RideRequest>
{
    public void Configure(EntityTypeBuilder<RideRequest> builder)
    {
        builder.ToTable(
            "ride_requests",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_ride_requests_estimated_trip_duration_positive",
                    "estimated_trip_duration_seconds > 0");
                table.HasCheckConstraint(
                    "ck_ride_requests_timing_requested_pickup_consistency",
                    "(timing = 'Immediate' AND requested_pickup_at IS NULL) OR " +
                    "(timing = 'Scheduled' AND requested_pickup_at IS NOT NULL)");
            });

        builder.HasKey(rideRequest => rideRequest.Id);

        builder.Property(rideRequest => rideRequest.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(rideRequest => rideRequest.PassengerId)
            .HasColumnName("passenger_id");

        builder.Property(rideRequest => rideRequest.ContactName)
            .HasColumnName("contact_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(rideRequest => rideRequest.ContactPhone)
            .HasColumnName("contact_phone")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(rideRequest => rideRequest.BookingSource)
            .HasColumnName("booking_source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rideRequest => rideRequest.Timing)
            .HasColumnName("timing")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rideRequest => rideRequest.PickupLatitude)
            .HasColumnName("pickup_latitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(rideRequest => rideRequest.PickupLongitude)
            .HasColumnName("pickup_longitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(rideRequest => rideRequest.DestinationLatitude)
            .HasColumnName("destination_latitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(rideRequest => rideRequest.DestinationLongitude)
            .HasColumnName("destination_longitude")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(rideRequest => rideRequest.RequiredVehicleType)
            .HasColumnName("required_vehicle_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rideRequest => rideRequest.RequestedPickupAt)
            .HasColumnName("requested_pickup_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(rideRequest => rideRequest.EstimatedTripDurationSeconds)
            .HasColumnName("estimated_trip_duration_seconds")
            .IsRequired();

        builder.Property(rideRequest => rideRequest.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Passenger>()
            .WithMany()
            .HasForeignKey(rideRequest => rideRequest.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
