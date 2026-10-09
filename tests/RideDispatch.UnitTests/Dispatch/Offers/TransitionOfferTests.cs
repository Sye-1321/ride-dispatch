using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Domain.Dispatch;

namespace RideDispatch.UnitTests.Dispatch.Offers;

public sealed class TransitionOfferTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("accept", OfferStatus.Accepted)]
    [InlineData("decline", OfferStatus.Declined)]
    [InlineData("withdraw", OfferStatus.Withdrawn)]
    public async Task Successful_action_saves_once_and_reads_clock_once(string action, OfferStatus expected)
    {
        var offer = CreateOffer();
        var store = new StubOfferStore(offer);
        var clock = new CountingTimeProvider(offer.ExpiresAt.AddTicks(-1));
        var result = await Execute(action, store, clock, offer.Id);
        Assert.Equal(TransitionOfferOutcome.Success, result.Outcome);
        Assert.Equal(expected, offer.Status);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, clock.ReadCount);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    [InlineData("withdraw")]
    public async Task Exact_expiry_marks_expired_saves_once_and_reads_clock_once(string action)
    {
        var offer = CreateOffer();
        var store = new StubOfferStore(offer);
        var clock = new CountingTimeProvider(offer.ExpiresAt);
        var result = await Execute(action, store, clock, offer.Id);
        Assert.Equal(TransitionOfferOutcome.OfferExpired, result.Outcome);
        Assert.Equal(OfferStatus.Expired, offer.Status);
        Assert.Equal(offer.ExpiresAt, offer.ResolvedAt);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, clock.ReadCount);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    [InlineData("withdraw")]
    public async Task Missing_offer_does_not_read_clock_or_save(string action)
    {
        var store = new StubOfferStore(null);
        var clock = new CountingTimeProvider(CreatedAt);
        var result = await Execute(action, store, clock, Guid.NewGuid());
        Assert.Equal(TransitionOfferOutcome.OfferNotFound, result.Outcome);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, clock.ReadCount);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    [InlineData("withdraw")]
    public async Task Terminal_offer_is_invalid_without_clock_read_or_save(string action)
    {
        var offer = CreateOffer();
        offer.Accept(CreatedAt.AddSeconds(1));
        var store = new StubOfferStore(offer);
        var clock = new CountingTimeProvider(CreatedAt.AddSeconds(2));
        var result = await Execute(action, store, clock, offer.Id);
        Assert.Equal(TransitionOfferOutcome.InvalidState, result.Outcome);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, clock.ReadCount);
    }

    private static Offer CreateOffer() => Offer.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(20), CreatedAt);

    private static Task<TransitionOfferResult> Execute(string action, StubOfferStore store, CountingTimeProvider clock, Guid id) => action switch
    {
        "accept" => new AcceptOffer(store, clock).ExecuteAsync(id, CancellationToken.None),
        "decline" => new DeclineOffer(store, clock).ExecuteAsync(id, CancellationToken.None),
        "withdraw" => new WithdrawOffer(store, clock).ExecuteAsync(id, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private sealed class StubOfferStore(Offer? offer) : IOfferStore
    {
        public int SaveCount { get; private set; }
        public Task<Offer?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(offer?.Id == id ? offer : null);
        public Task SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.CompletedTask; }
        public Task AddAsync(Offer value, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Offer?> FindByAllocationRunIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CountingTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public int ReadCount { get; private set; }
        public override DateTimeOffset GetUtcNow() { ReadCount++; return now; }
    }
}
