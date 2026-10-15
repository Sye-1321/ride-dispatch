using RideDispatch.Domain.Dispatch;

namespace RideDispatch.Application.Dispatch.Offers;

public sealed class RideOfferAvailability(IOfferStore offerStore)
{
    public async Task<bool> CanCreateAsync(
        Guid rideRequestId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 2;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var blocking = await offerStore.FindActionableByRideRequestIdAsync(
                rideRequestId, cancellationToken);
            if (blocking is null)
            {
                return true;
            }

            if (blocking.Status == OfferStatus.Accepted || now < blocking.ExpiresAt)
            {
                return false;
            }

            blocking.Expire(now);
            try
            {
                await offerStore.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (OfferStateConcurrencyException)
            {
                // The next attempt reloads and evaluates the authoritative state.
            }
        }

        return false;
    }
}
