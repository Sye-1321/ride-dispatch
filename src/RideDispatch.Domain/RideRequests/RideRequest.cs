using RideDispatch.Domain.Vehicles;

namespace RideDispatch.Domain.RideRequests;

public sealed class RideRequest
{
    private RideRequest()
    {
        ContactName = null!;
        ContactPhone = null!;
    }

    private RideRequest(
        Guid id,
        Guid? passengerId,
        string contactName,
        string contactPhone,
        BookingSource bookingSource,
        RideTiming timing,
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude,
        VehicleType requiredVehicleType,
        DateTimeOffset? requestedPickupAt,
        int estimatedTripDurationSeconds,
        DateTimeOffset createdAt)
    {
        Id = id;
        PassengerId = passengerId;
        ContactName = contactName;
        ContactPhone = contactPhone;
        BookingSource = bookingSource;
        Timing = timing;
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        DestinationLatitude = destinationLatitude;
        DestinationLongitude = destinationLongitude;
        RequiredVehicleType = requiredVehicleType;
        RequestedPickupAt = requestedPickupAt;
        EstimatedTripDurationSeconds = estimatedTripDurationSeconds;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid? PassengerId { get; private set; }

    public string ContactName { get; private set; }

    public string ContactPhone { get; private set; }

    public BookingSource BookingSource { get; private set; }

    public RideTiming Timing { get; private set; }

    public double PickupLatitude { get; private set; }

    public double PickupLongitude { get; private set; }

    public double DestinationLatitude { get; private set; }

    public double DestinationLongitude { get; private set; }

    public VehicleType RequiredVehicleType { get; private set; }

    public DateTimeOffset? RequestedPickupAt { get; private set; }

    public int EstimatedTripDurationSeconds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static RideRequest Create(
        Guid? passengerId,
        string contactName,
        string contactPhone,
        BookingSource bookingSource,
        RideTiming timing,
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude,
        VehicleType requiredVehicleType,
        DateTimeOffset? requestedPickupAt,
        int estimatedTripDurationSeconds,
        DateTimeOffset currentUtcTime)
    {
        var normalizedContactName = NormalizeRequired(contactName, 200, nameof(contactName));
        var normalizedContactPhone = NormalizeRequired(contactPhone, 32, nameof(contactPhone));

        ValidateCoordinate(pickupLatitude, -90, 90, nameof(pickupLatitude));
        ValidateCoordinate(pickupLongitude, -180, 180, nameof(pickupLongitude));
        ValidateCoordinate(destinationLatitude, -90, 90, nameof(destinationLatitude));
        ValidateCoordinate(destinationLongitude, -180, 180, nameof(destinationLongitude));

        if (estimatedTripDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedTripDurationSeconds),
                "Estimated trip duration must be greater than zero.");
        }

        if (!Enum.IsDefined(bookingSource))
        {
            throw new ArgumentOutOfRangeException(nameof(bookingSource));
        }

        if (!Enum.IsDefined(requiredVehicleType))
        {
            throw new ArgumentOutOfRangeException(nameof(requiredVehicleType));
        }

        DateTimeOffset? normalizedRequestedPickupAt = timing switch
        {
            RideTiming.Immediate when requestedPickupAt is null => null,
            RideTiming.Immediate => throw new ArgumentException(
                "Immediate ride requests must not specify a requested pickup time.",
                nameof(requestedPickupAt)),
            RideTiming.Scheduled when requestedPickupAt is null => throw new ArgumentException(
                "Scheduled ride requests require a requested pickup time.",
                nameof(requestedPickupAt)),
            RideTiming.Scheduled when requestedPickupAt <= currentUtcTime => throw new ArgumentException(
                "Scheduled pickup time must be later than the current time.",
                nameof(requestedPickupAt)),
            RideTiming.Scheduled => requestedPickupAt.Value.ToUniversalTime(),
            _ => throw new ArgumentOutOfRangeException(nameof(timing)),
        };

        return new RideRequest(
            Guid.CreateVersion7(),
            passengerId,
            normalizedContactName,
            normalizedContactPhone,
            bookingSource,
            timing,
            pickupLatitude,
            pickupLongitude,
            destinationLatitude,
            destinationLongitude,
            requiredVehicleType,
            normalizedRequestedPickupAt,
            estimatedTripDurationSeconds,
            currentUtcTime.ToUniversalTime());
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Contact value must not be empty or whitespace.", parameterName);
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Contact value must not exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static void ValidateCoordinate(
        double value,
        double minimum,
        double maximum,
        string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Coordinate must be finite and between {minimum} and {maximum}.");
        }
    }
}
