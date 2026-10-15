using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Offers;

public sealed class RideOfferAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Unexpired_pending_offer_blocks_creation()
    {
        var offer = Pending(Now);
        var store = new Store(offer);

        Assert.False(await new RideOfferAvailability(store).CanCreateAsync(
            offer.RideRequestId, Now, default));
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Expired_pending_offer_is_persisted_before_creation_is_allowed()
    {
        var offer = Pending(Now.AddSeconds(-20));
        var store = new Store(offer);

        Assert.True(await new RideOfferAvailability(store).CanCreateAsync(
            offer.RideRequestId, Now, default));
        Assert.Equal(OfferStatus.Expired, offer.Status);
        Assert.Equal(Now, offer.ResolvedAt);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Accepted_offer_remains_blocking()
    {
        var offer = Pending(Now.AddSeconds(-1));
        offer.Accept(Now);

        Assert.False(await new RideOfferAvailability(new Store(offer)).CanCreateAsync(
            offer.RideRequestId, Now.AddYears(1), default));
    }

    [Fact]
    public async Task Concurrency_conflict_reloads_accepted_offer_and_returns_blocked()
    {
        var stale = Pending(Now.AddSeconds(-20));
        var accepted = Pending(Now.AddSeconds(-1), stale.RideRequestId);
        accepted.Accept(Now);
        var store = new Store([stale, accepted], concurrencyFailures: 1);

        Assert.False(await new RideOfferAvailability(store).CanCreateAsync(
            stale.RideRequestId, Now, default));
        Assert.Equal(2, store.FindCount);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Repeated_concurrency_conflicts_stop_after_two_attempts()
    {
        var first = Pending(Now.AddSeconds(-20));
        var second = Pending(Now.AddSeconds(-20), first.RideRequestId);
        var store = new Store([first, second], concurrencyFailures: 2);

        Assert.False(await new RideOfferAvailability(store).CanCreateAsync(
            first.RideRequestId, Now, default));
        Assert.Equal(2, store.FindCount);
        Assert.Equal(2, store.SaveCount);
    }

    [Theory]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Withdrawn)]
    [InlineData(OfferStatus.Expired)]
    public async Task Non_actionable_offers_do_not_block_creation(OfferStatus status)
    {
        var offer = Pending(Now.AddSeconds(-20));
        if (status == OfferStatus.Declined) offer.Decline(Now.AddSeconds(-11));
        else if (status == OfferStatus.Withdrawn) offer.Withdraw(Now.AddSeconds(-11));
        else offer.Expire(Now.AddSeconds(-10));

        Assert.True(await new RideOfferAvailability(new Store(null)).CanCreateAsync(
            offer.RideRequestId, Now, default));
    }

    private static Offer Pending(DateTimeOffset createdAt, Guid? rideRequestId = null) =>
        Offer.Create(Guid.NewGuid(), rideRequestId ?? Guid.NewGuid(), Guid.NewGuid(),
            TimeSpan.FromSeconds(10), createdAt);

    private sealed class Store : IOfferStore
    {
        private readonly Queue<Offer?> actionableOffers;
        private int concurrencyFailures;

        public Store(Offer? actionable)
            : this([actionable], 0)
        {
        }

        public Store(IEnumerable<Offer?> actionableOffers, int concurrencyFailures)
        {
            this.actionableOffers = new Queue<Offer?>(actionableOffers);
            this.concurrencyFailures = concurrencyFailures;
        }

        public int FindCount { get; private set; }
        public int SaveCount { get; private set; }
        public Task<Offer?> FindActionableByRideRequestIdAsync(Guid id, CancellationToken ct)
        {
            FindCount++;
            var actionable = actionableOffers.Dequeue();
            return Task.FromResult(actionable?.Status is OfferStatus.Pending or OfferStatus.Accepted
                ? actionable
                : null);
        }
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            if (concurrencyFailures-- > 0)
            {
                throw new OfferStateConcurrencyException(new Exception());
            }
            return Task.CompletedTask;
        }
        public Task AddAsync(Offer value, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
