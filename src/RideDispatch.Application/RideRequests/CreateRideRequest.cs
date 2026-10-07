using RideDispatch.Application.Passengers;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Application.RideRequests;

public sealed record CreateRideRequestInput(
    Guid? PassengerId,
    string? ContactName,
    string? ContactPhone,
    BookingSource BookingSource,
    RideTiming Timing,
    double PickupLatitude,
    double PickupLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    VehicleType RequiredVehicleType,
    DateTimeOffset? RequestedPickupAt,
    int EstimatedTripDurationSeconds);

public enum CreateRideRequestOutcome
{
    Success,
    PassengerNotFound,
}

public sealed record CreateRideRequestResult(
    CreateRideRequestOutcome Outcome,
    RideRequestView? RideRequest = null);

public sealed class CreateRideRequest(
    IPassengerStore passengerStore,
    IRideRequestStore rideRequestStore,
    TimeProvider timeProvider)
{
    public async Task<CreateRideRequestResult> ExecuteAsync(
        CreateRideRequestInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateContactMode(input);

        string contactName;
        string contactPhone;

        if (input.PassengerId is Guid passengerId)
        {
            var passenger = await passengerStore.FindByIdAsync(passengerId, cancellationToken);
            if (passenger is null)
            {
                return new(CreateRideRequestOutcome.PassengerNotFound);
            }

            contactName = passenger.Name;
            contactPhone = passenger.PhoneNumber;
        }
        else
        {
            contactName = input.ContactName!;
            contactPhone = input.ContactPhone!;
        }

        var rideRequest = RideRequest.Create(
            input.PassengerId,
            contactName,
            contactPhone,
            input.BookingSource,
            input.Timing,
            input.PickupLatitude,
            input.PickupLongitude,
            input.DestinationLatitude,
            input.DestinationLongitude,
            input.RequiredVehicleType,
            input.RequestedPickupAt,
            input.EstimatedTripDurationSeconds,
            timeProvider.GetUtcNow());

        await rideRequestStore.AddAsync(rideRequest, cancellationToken);
        await rideRequestStore.SaveChangesAsync(cancellationToken);

        return new(CreateRideRequestOutcome.Success, RideRequestView.FromRideRequest(rideRequest));
    }

    private static void ValidateContactMode(CreateRideRequestInput input)
    {
        var hasContactName = input.ContactName is not null;
        var hasContactPhone = input.ContactPhone is not null;

        if (input.PassengerId is not null && (hasContactName || hasContactPhone))
        {
            throw new ArgumentException(
                "Contact name and phone must not be supplied with a passenger ID.");
        }

        if (input.PassengerId is null && (!hasContactName || !hasContactPhone))
        {
            throw new ArgumentException(
                "Direct-contact ride requests require both contact name and contact phone.");
        }
    }
}
