using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.RideRequests;

public sealed record RideRequestView(
    Guid Id,
    Guid? PassengerId,
    string ContactName,
    string ContactPhone,
    BookingSource BookingSource,
    RideTiming Timing,
    double PickupLatitude,
    double PickupLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    VehicleType RequiredVehicleType,
    DateTimeOffset? RequestedPickupAt,
    int EstimatedTripDurationSeconds,
    DateTimeOffset CreatedAt)
{
    public static RideRequestView FromRideRequest(RideRequest rideRequest) =>
        new(
            rideRequest.Id,
            rideRequest.PassengerId,
            rideRequest.ContactName,
            rideRequest.ContactPhone,
            rideRequest.BookingSource,
            rideRequest.Timing,
            rideRequest.PickupLatitude,
            rideRequest.PickupLongitude,
            rideRequest.DestinationLatitude,
            rideRequest.DestinationLongitude,
            rideRequest.RequiredVehicleType,
            rideRequest.RequestedPickupAt,
            rideRequest.EstimatedTripDurationSeconds,
            rideRequest.CreatedAt);
}
