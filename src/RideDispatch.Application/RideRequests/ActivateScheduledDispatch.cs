namespace RideDispatch.Application.RideRequests;

public enum ActivateScheduledDispatchOutcome
{
    Success,
    AlreadyActivated,
    RideRequestNotFound,
    RideNotScheduled,
    ActivationTooEarly,
    ScheduledPickupElapsed,
}

public sealed record ActivateScheduledDispatchResult(
    ActivateScheduledDispatchOutcome Outcome,
    RideRequestView? RideRequest = null);

public sealed class RideRequestActivationConcurrencyException(Exception innerException)
    : Exception("Scheduled dispatch activation lost an optimistic concurrency race.", innerException);

public sealed class ActivateScheduledDispatch(
    IRideRequestStore rideRequestStore,
    ScheduledDispatchPolicy policy,
    TimeProvider timeProvider)
{
    public async Task<ActivateScheduledDispatchResult> ExecuteAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var rideRequest = await rideRequestStore.FindByIdTrackedAsync(rideRequestId, cancellationToken);
        if (rideRequest is null)
        {
            return new(ActivateScheduledDispatchOutcome.RideRequestNotFound);
        }

        if (rideRequest.Timing != Domain.RideRequests.RideTiming.Scheduled)
        {
            return new(ActivateScheduledDispatchOutcome.RideNotScheduled);
        }

        if (rideRequest.DispatchActivatedAt is not null)
        {
            return new(ActivateScheduledDispatchOutcome.AlreadyActivated, RideRequestView.FromRideRequest(rideRequest));
        }

        var now = timeProvider.GetUtcNow();
        var pickupAt = rideRequest.RequestedPickupAt!.Value;
        if (now < pickupAt - policy.ActivationLeadTime)
        {
            return new(ActivateScheduledDispatchOutcome.ActivationTooEarly);
        }

        if (now > pickupAt)
        {
            return new(ActivateScheduledDispatchOutcome.ScheduledPickupElapsed);
        }

        rideRequest.ActivateDispatch(now, policy.ActivationLeadTime);
        try
        {
            await rideRequestStore.SaveChangesAsync(cancellationToken);
        }
        catch (RideRequestActivationConcurrencyException)
        {
            var current = await rideRequestStore.FindByIdAsync(rideRequestId, cancellationToken);
            if (current?.DispatchActivatedAt is not null)
            {
                return new(ActivateScheduledDispatchOutcome.AlreadyActivated, RideRequestView.FromRideRequest(current));
            }

            throw;
        }

        return new(ActivateScheduledDispatchOutcome.Success, RideRequestView.FromRideRequest(rideRequest));
    }
}
