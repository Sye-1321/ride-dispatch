namespace RideDispatch.Application.RideRequests;

public sealed class ScheduledDispatchPolicy
{
    public ScheduledDispatchPolicy(TimeSpan activationLeadTime)
    {
        if (activationLeadTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(activationLeadTime));
        }

        ActivationLeadTime = activationLeadTime;
    }

    public TimeSpan ActivationLeadTime { get; }
}
