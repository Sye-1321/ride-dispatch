using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Offers;

public sealed record OfferView(
    Guid Id,
    Guid AllocationRunId,
    Guid DriverId,
    OfferStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ResolvedAt)
{
    public static OfferView FromOffer(Offer offer) =>
        new(offer.Id, offer.AllocationRunId, offer.DriverId, offer.Status, offer.CreatedAt, offer.ExpiresAt, offer.ResolvedAt);
}
