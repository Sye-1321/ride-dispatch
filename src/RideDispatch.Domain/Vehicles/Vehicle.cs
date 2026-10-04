namespace RideDispatch.Domain.Vehicles;

public sealed class Vehicle
{
    private Vehicle()
    {
        PlateNumber = null!;
    }

    private Vehicle(Guid id, Guid driverId, VehicleType type, string plateNumber)
    {
        Id = id;
        DriverId = driverId;
        Type = type;
        PlateNumber = plateNumber;
    }

    public Guid Id { get; private set; }

    public Guid DriverId { get; private set; }

    public VehicleType Type { get; private set; }

    public string PlateNumber { get; private set; }

    public static Vehicle Create(Guid driverId, VehicleType type, string plateNumber)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver ID must not be empty.", nameof(driverId));
        }

        ArgumentNullException.ThrowIfNull(plateNumber);

        var normalizedPlateNumber = plateNumber.Trim();
        if (normalizedPlateNumber.Length == 0)
        {
            throw new ArgumentException("Plate number must not be empty or whitespace.", nameof(plateNumber));
        }

        if (normalizedPlateNumber.Length > 32)
        {
            throw new ArgumentException("Plate number must not exceed 32 characters.", nameof(plateNumber));
        }

        return new Vehicle(Guid.CreateVersion7(), driverId, type, normalizedPlateNumber);
    }
}
