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
        Assert.Null(rideRequest.DispatchActivatedAt);
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
        Assert.Null(rideRequest.DispatchActivatedAt);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(10)]
    [InlineData(0)]
    public void Scheduled_dispatch_activates_inside_inclusive_window(int minutesBeforePickup)
    {
        var pickup = CurrentTime.AddHours(1);
        var rideRequest = CreateScheduledRideRequest(pickup);
        var activationTime = pickup.AddMinutes(-minutesBeforePickup);

        rideRequest.ActivateDispatch(activationTime, TimeSpan.FromMinutes(15));

        Assert.Equal(activationTime, rideRequest.DispatchActivatedAt);
    }

    [Fact]
    public void Scheduled_dispatch_normalizes_activation_to_utc_and_cannot_activate_twice()
    {
        var pickup = CurrentTime.AddHours(1);
        var rideRequest = CreateScheduledRideRequest(pickup);
        var activation = new DateTimeOffset(2026, 10, 4, 15, 50, 0, TimeSpan.FromHours(3));

        rideRequest.ActivateDispatch(activation, TimeSpan.FromMinutes(15));

        Assert.Equal(activation.ToUniversalTime(), rideRequest.DispatchActivatedAt);
        Assert.Throws<InvalidOperationException>(() =>
            rideRequest.ActivateDispatch(pickup, TimeSpan.FromMinutes(15)));
        Assert.Equal(activation.ToUniversalTime(), rideRequest.DispatchActivatedAt);
    }

    [Fact]
    public void Scheduled_dispatch_rejects_outside_window_and_before_creation()
    {
        var pickup = CurrentTime.AddHours(1);
        Assert.Throws<InvalidOperationException>(() =>
            CreateScheduledRideRequest(pickup)
                .ActivateDispatch(pickup.AddMinutes(-16), TimeSpan.FromMinutes(15)));
        Assert.Throws<InvalidOperationException>(() =>
            CreateScheduledRideRequest(pickup)
                .ActivateDispatch(pickup.AddTicks(1), TimeSpan.FromMinutes(15)));
        Assert.Throws<InvalidOperationException>(() =>
            CreateScheduledRideRequest(CurrentTime.AddMinutes(10))
                .ActivateDispatch(CurrentTime.AddTicks(-1), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Immediate_dispatch_activation_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateRideRequest().ActivateDispatch(CurrentTime, TimeSpan.FromMinutes(15)));
    }

    private static RideRequest CreateScheduledRideRequest(DateTimeOffset requestedPickupAt) =>
        CreateRideRequest(timing: RideTiming.Scheduled, requestedPickupAt: requestedPickupAt);

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
