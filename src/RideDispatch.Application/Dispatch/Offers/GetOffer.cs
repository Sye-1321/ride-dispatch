namespace RideDispatch.Application.Dispatch.Offers;

public sealed class GetOffer(IOfferStore offerStore)
{
    public async Task<OfferView?> ExecuteAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await offerStore.FindByIdReadOnlyAsync(offerId, cancellationToken);
        return offer is null ? null : OfferView.FromOffer(offer);
    }
}
