using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Offers;

public sealed class OfferTests
{
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 9, 12, 0, 0, TimeSpan.FromHours(3));
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(20);

    [Fact]
    public void Creation_sets_pending_uuid7_and_normalized_times()
    {
        var offer = Create();

        Assert.Equal(7, offer.Id.Version);
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Equal(TimeSpan.Zero, offer.CreatedAt.Offset);
        Assert.Equal(CurrentTime.ToUniversalTime(), offer.CreatedAt);
        Assert.Equal(offer.CreatedAt + Ttl, offer.ExpiresAt);
        Assert.Null(offer.ResolvedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Creation_rejects_empty_identifiers(bool emptyAllocationRunId)
    {
        var allocationRunId = emptyAllocationRunId ? Guid.Empty : Guid.NewGuid();
        var driverId = emptyAllocationRunId ? Guid.NewGuid() : Guid.Empty;
        Assert.Throws<ArgumentException>(() => Offer.Create(allocationRunId, driverId, Ttl, CurrentTime));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Creation_rejects_non_positive_ttl(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Offer.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(seconds), CurrentTime));

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Pending_offer_can_resolve_before_expiry(OfferStatus target)
    {
        var offer = Create();
        var identifiers = (offer.AllocationRunId, offer.DriverId, offer.CreatedAt, offer.ExpiresAt);
        var resolvedAt = offer.ExpiresAt.AddTicks(-1);

        Transition(offer, target, resolvedAt);

        Assert.Equal(target, offer.Status);
        Assert.Equal(resolvedAt, offer.ResolvedAt);
        Assert.Equal(identifiers, (offer.AllocationRunId, offer.DriverId, offer.CreatedAt, offer.ExpiresAt));
    }

    [Fact]
    public void Pending_offer_expires_at_exact_boundary()
    {
        var offer = Create();
        offer.Expire(offer.ExpiresAt);
        Assert.Equal(OfferStatus.Expired, offer.Status);
        Assert.Equal(offer.ExpiresAt, offer.ResolvedAt);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Requested_actions_are_rejected_at_exact_expiry_boundary(OfferStatus target)
    {
        var offer = Create();
        Assert.Throws<InvalidOperationException>(() => Transition(offer, target, offer.ExpiresAt));
        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Null(offer.ResolvedAt);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Offer_cannot_resolve_before_creation(OfferStatus target)
    {
        var offer = Create();

        Assert.Throws<InvalidOperationException>(() =>
            Transition(offer, target, offer.CreatedAt.AddTicks(-1)));

        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Null(offer.ResolvedAt);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Declined)]
    [InlineData(OfferStatus.Expired)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Terminal_states_cannot_transition(OfferStatus initial)
    {
        var offer = Create();
        Transition(offer, initial, initial == OfferStatus.Expired ? offer.ExpiresAt : offer.ExpiresAt.AddTicks(-1));
        Assert.Throws<InvalidOperationException>(() => offer.Accept(offer.ExpiresAt.AddSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => offer.Decline(offer.ExpiresAt.AddSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => offer.Expire(offer.ExpiresAt.AddSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => offer.Withdraw(offer.ExpiresAt.AddSeconds(1)));
    }

    private static Offer Create() => Offer.Create(Guid.NewGuid(), Guid.NewGuid(), Ttl, CurrentTime);

    private static void Transition(Offer offer, OfferStatus target, DateTimeOffset at)
    {
        switch (target)
        {
            case OfferStatus.Accepted: offer.Accept(at); break;
            case OfferStatus.Declined: offer.Decline(at); break;
            case OfferStatus.Expired: offer.Expire(at); break;
            case OfferStatus.Withdrawn: offer.Withdraw(at); break;
            default: throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
