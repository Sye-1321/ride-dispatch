using RideDispatch.Application.Passengers;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Passengers;
using RideDispatch.Domain.RideRequests;
using RideDispatch.Domain.Vehicles;

namespace RideDispatch.UnitTests.RideRequests;

public sealed class CreateRideRequestTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Registered_passenger_is_loaded_and_snapshotted()
    {
        var passenger = Passenger.Create("Sara Tesfaye", "+251911234567");
        var passengerStore = new StubPassengerStore(passenger);
        var rideRequestStore = new CapturingRideRequestStore();
        var useCase = CreateUseCase(passengerStore, rideRequestStore);

        var result = await useCase.ExecuteAsync(
            CreateInput(passengerId: passenger.Id),
            CancellationToken.None);

        Assert.Equal(CreateRideRequestOutcome.Success, result.Outcome);
        Assert.Equal(passenger.Id, result.RideRequest!.PassengerId);
        Assert.Equal(passenger.Name, result.RideRequest.ContactName);
        Assert.Equal(passenger.PhoneNumber, result.RideRequest.ContactPhone);
        Assert.True(rideRequestStore.AddWasCalled);
    }

    [Fact]
    public async Task Missing_passenger_returns_not_found_without_persisting()
    {
        var passengerStore = new StubPassengerStore(null);
        var rideRequestStore = new CapturingRideRequestStore();
        var useCase = CreateUseCase(passengerStore, rideRequestStore);

        var result = await useCase.ExecuteAsync(
            CreateInput(passengerId: Guid.CreateVersion7()),
            CancellationToken.None);

        Assert.Equal(CreateRideRequestOutcome.PassengerNotFound, result.Outcome);
        Assert.False(rideRequestStore.AddWasCalled);
    }

    [Fact]
    public async Task Direct_contact_does_not_load_passenger_and_is_stored()
    {
        var passengerStore = new StubPassengerStore(null);
        var rideRequestStore = new CapturingRideRequestStore();
        var useCase = CreateUseCase(passengerStore, rideRequestStore);

        var result = await useCase.ExecuteAsync(
            CreateInput(contactName: "Hana Gebru", contactPhone: "+251911000000"),
            CancellationToken.None);

        Assert.Equal(CreateRideRequestOutcome.Success, result.Outcome);
        Assert.Equal(0, passengerStore.FindCallCount);
        Assert.Null(result.RideRequest!.PassengerId);
        Assert.Equal("Hana Gebru", result.RideRequest.ContactName);
        Assert.Equal("+251911000000", result.RideRequest.ContactPhone);
    }

    [Fact]
    public async Task Ambiguous_contact_mode_is_rejected_before_persistence()
    {
        var passengerStore = new StubPassengerStore(null);
        var rideRequestStore = new CapturingRideRequestStore();
        var useCase = CreateUseCase(passengerStore, rideRequestStore);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            CreateInput(
                passengerId: Guid.CreateVersion7(),
                contactName: "Hana Gebru",
                contactPhone: "+251911000000"),
            CancellationToken.None));

        Assert.Equal(0, passengerStore.FindCallCount);
        Assert.False(rideRequestStore.AddWasCalled);
    }

    private static CreateRideRequest CreateUseCase(
        StubPassengerStore passengerStore,
        CapturingRideRequestStore rideRequestStore) =>
        new(passengerStore, rideRequestStore, new FixedTimeProvider(CurrentTime));

    private static CreateRideRequestInput CreateInput(
        Guid? passengerId = null,
        string? contactName = null,
        string? contactPhone = null) =>
        new(
            passengerId,
            contactName,
            contactPhone,
            BookingSource.App,
            RideTiming.Immediate,
            9.03,
            38.74,
            9.01,
            38.78,
            VehicleType.Standard,
            null,
            1200);

    private sealed class StubPassengerStore(Passenger? passenger) : IPassengerStore
    {
        public int FindCallCount { get; private set; }

        public Task<Passenger?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            FindCallCount++;
            return Task.FromResult(passenger?.Id == id ? passenger : null);
        }

        public Task AddAsync(Passenger passenger, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingRideRequestStore : IRideRequestStore
    {
        public bool AddWasCalled { get; private set; }

        public Task AddAsync(RideRequest rideRequest, CancellationToken cancellationToken)
        {
            AddWasCalled = true;
            return Task.CompletedTask;
        }

        public Task<RideRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RideRequest>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
