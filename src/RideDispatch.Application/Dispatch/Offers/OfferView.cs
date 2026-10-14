using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Offers;

public sealed record OfferView(
    Guid Id,
    Guid AllocationRunId,
    Guid DriverId,
    OfferStatus Status,
    OfferSelectionMode SelectionMode,
    string? OverrideReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ResolvedAt)
{
    public static OfferView FromOffer(Offer offer) =>
        new(offer.Id, offer.AllocationRunId, offer.DriverId, offer.Status, offer.SelectionMode,
            offer.OverrideReason, offer.CreatedAt, offer.ExpiresAt, offer.ResolvedAt);
}
