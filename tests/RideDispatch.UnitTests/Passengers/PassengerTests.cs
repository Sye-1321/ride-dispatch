using RideDispatch.Domain.Passengers;

namespace RideDispatch.UnitTests.Passengers;

public sealed class PassengerTests
{
    [Fact]
    public void Create_trims_fields_and_generates_id()
    {
        var passenger = Passenger.Create("  Sara Tesfaye  ", "  +251911234567  ");

        Assert.NotEqual(Guid.Empty, passenger.Id);
        Assert.Equal("Sara Tesfaye", passenger.Name);
        Assert.Equal("+251911234567", passenger.PhoneNumber);
    }

    [Theory]
    [InlineData("", "+251911234567")]
    [InlineData("   ", "+251911234567")]
    [InlineData("Sara", "")]
    [InlineData("Sara", "   ")]
    public void Create_rejects_blank_fields(string name, string phoneNumber)
    {
        Assert.Throws<ArgumentException>(() => Passenger.Create(name, phoneNumber));
    }

    [Fact]
    public void Create_rejects_over_length_name()
    {
        Assert.Throws<ArgumentException>(() =>
            Passenger.Create(new string('a', 201), "+251911234567"));
    }

    [Fact]
    public void Create_rejects_over_length_phone_number()
    {
        Assert.Throws<ArgumentException>(() =>
            Passenger.Create("Sara", new string('1', 33)));
    }
}
