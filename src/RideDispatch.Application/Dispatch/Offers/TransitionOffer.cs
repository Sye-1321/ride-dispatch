using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Offers;

public enum TransitionOfferOutcome { Success, OfferNotFound, OfferExpired, InvalidState }
public sealed record TransitionOfferResult(TransitionOfferOutcome Outcome, OfferView? Offer = null);

public abstract class TransitionOffer(IOfferStore offerStore, TimeProvider timeProvider)
{
    protected async Task<TransitionOfferResult> ExecuteAsync(
        Guid offerId,
        Action<Offer, DateTimeOffset> transition,
        CancellationToken cancellationToken)
    {
        var offer = await offerStore.FindByIdAsync(offerId, cancellationToken);
        if (offer is null)
        {
            return new(TransitionOfferOutcome.OfferNotFound);
        }

        if (offer.Status != OfferStatus.Pending)
        {
            return new(TransitionOfferOutcome.InvalidState, OfferView.FromOffer(offer));
        }

        var currentTime = timeProvider.GetUtcNow();
        if (currentTime >= offer.ExpiresAt)
        {
            offer.Expire(currentTime);
            await offerStore.SaveChangesAsync(cancellationToken);
            return new(TransitionOfferOutcome.OfferExpired, OfferView.FromOffer(offer));
        }

        transition(offer, currentTime);
        await offerStore.SaveChangesAsync(cancellationToken);
        return new(TransitionOfferOutcome.Success, OfferView.FromOffer(offer));
    }
}

public sealed class AcceptOffer(IOfferStore store, TimeProvider clock) : TransitionOffer(store, clock)
{
    public Task<TransitionOfferResult> ExecuteAsync(Guid id, CancellationToken ct) =>
        base.ExecuteAsync(id, static (offer, now) => offer.Accept(now), ct);
}

public sealed class DeclineOffer(IOfferStore store, TimeProvider clock) : TransitionOffer(store, clock)
{
    public Task<TransitionOfferResult> ExecuteAsync(Guid id, CancellationToken ct) =>
        base.ExecuteAsync(id, static (offer, now) => offer.Decline(now), ct);
}

public sealed class WithdrawOffer(IOfferStore store, TimeProvider clock) : TransitionOffer(store, clock)
{
    public Task<TransitionOfferResult> ExecuteAsync(Guid id, CancellationToken ct) =>
        base.ExecuteAsync(id, static (offer, now) => offer.Withdraw(now), ct);
}
