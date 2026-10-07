using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.RideRequests;

public sealed class RideRequestTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Immediate_request_retains_creation_data()
    {
        var rideRequest = CreateRideRequest();

        Assert.Null(rideRequest.RequestedPickupAt);
        Assert.Equal(CurrentTime, rideRequest.CreatedAt);
        Assert.Equal(9.03, rideRequest.PickupLatitude);
        Assert.Equal(38.74, rideRequest.PickupLongitude);
        Assert.Equal(9.01, rideRequest.DestinationLatitude);
        Assert.Equal(38.78, rideRequest.DestinationLongitude);
        Assert.Equal(VehicleType.Standard, rideRequest.RequiredVehicleType);
        Assert.Equal(1200, rideRequest.EstimatedTripDurationSeconds);
    }

    [Fact]
    public void Immediate_request_rejects_requested_pickup_time()
    {
        Assert.Throws<ArgumentException>(() => CreateRideRequest(
            requestedPickupAt: CurrentTime.AddHours(1)));
    }

    [Fact]
    public void Scheduled_request_requires_requested_pickup_time()
    {
        Assert.Throws<ArgumentException>(() => CreateRideRequest(timing: RideTiming.Scheduled));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Scheduled_request_rejects_nonfuture_time(int minuteOffset)
    {
        Assert.Throws<ArgumentException>(() => CreateRideRequest(
            timing: RideTiming.Scheduled,
            requestedPickupAt: CurrentTime.AddMinutes(minuteOffset)));
    }

    [Fact]
    public void Scheduled_request_stores_future_time_normalized_to_utc()
    {
        var requestedPickupAt = new DateTimeOffset(2026, 10, 4, 18, 0, 0, TimeSpan.FromHours(3));

        var rideRequest = CreateRideRequest(
            timing: RideTiming.Scheduled,
            requestedPickupAt: requestedPickupAt);

        Assert.Equal(TimeSpan.Zero, rideRequest.RequestedPickupAt!.Value.Offset);
        Assert.Equal(requestedPickupAt.ToUniversalTime(), rideRequest.RequestedPickupAt);
    }

    [Theory]
    [InlineData(double.NaN, 38.74, 9.01, 38.78)]
    [InlineData(91, 38.74, 9.01, 38.78)]
    [InlineData(9.03, double.PositiveInfinity, 9.01, 38.78)]
    [InlineData(9.03, 38.74, -91, 38.78)]
    [InlineData(9.03, 38.74, 9.01, -181)]
    public void Request_rejects_invalid_coordinates(
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRideRequest(
            pickupLatitude: pickupLatitude,
            pickupLongitude: pickupLongitude,
            destinationLatitude: destinationLatitude,
            destinationLongitude: destinationLongitude));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Request_rejects_nonpositive_estimated_duration(int durationSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRideRequest(
            estimatedTripDurationSeconds: durationSeconds));
    }

    [Fact]
    public void Request_trims_contact_fields()
    {
        var rideRequest = CreateRideRequest(contactName: "  Hana Gebru  ", contactPhone: "  +251911000000  ");

        Assert.Equal("Hana Gebru", rideRequest.ContactName);
        Assert.Equal("+251911000000", rideRequest.ContactPhone);
    }

    private static RideRequest CreateRideRequest(
        string contactName = "Hana Gebru",
        string contactPhone = "+251911000000",
        RideTiming timing = RideTiming.Immediate,
        double pickupLatitude = 9.03,
        double pickupLongitude = 38.74,
        double destinationLatitude = 9.01,
        double destinationLongitude = 38.78,
        DateTimeOffset? requestedPickupAt = null,
        int estimatedTripDurationSeconds = 1200) =>
        RideRequest.Create(
            null,
            contactName,
            contactPhone,
            BookingSource.CallCenter,
            timing,
            pickupLatitude,
            pickupLongitude,
            destinationLatitude,
            destinationLongitude,
            VehicleType.Standard,
            requestedPickupAt,
            estimatedTripDurationSeconds,
            CurrentTime);
}
