using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.Vehicles;

public sealed class VehicleTests
{
    [Fact]
    public void Create_generates_identity_and_retains_normalized_details()
    {
        var driverId = Guid.CreateVersion7();

        var vehicle = Vehicle.Create(driverId, VehicleType.Standard, "  2-B12345  ");

        Assert.NotEqual(Guid.Empty, vehicle.Id);
        Assert.Equal(driverId, vehicle.DriverId);
        Assert.Equal(VehicleType.Standard, vehicle.Type);
        Assert.Equal("2-B12345", vehicle.PlateNumber);
    }

    [Fact]
    public void Create_rejects_whitespace_only_plate_number()
    {
        Assert.Throws<ArgumentException>(() =>
            Vehicle.Create(Guid.CreateVersion7(), VehicleType.Standard, "   "));
    }

    [Fact]
    public void Create_rejects_plate_number_longer_than_32_characters()
    {
        Assert.Throws<ArgumentException>(() =>
            Vehicle.Create(Guid.CreateVersion7(), VehicleType.Standard, new string('A', 33)));
    }

    [Fact]
    public void Create_rejects_empty_driver_id()
    {
        Assert.Throws<ArgumentException>(() =>
            Vehicle.Create(Guid.Empty, VehicleType.Standard, "2-B12345"));
    }
}
