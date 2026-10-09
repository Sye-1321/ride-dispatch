namespace RideDispatch.Application.Dispatch.Offers;

public sealed class OfferPolicy
{
    public OfferPolicy(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Offer TTL must be greater than zero.");
        }

        Ttl = ttl;
    }

    public TimeSpan Ttl { get; }
}
