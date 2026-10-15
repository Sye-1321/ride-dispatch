using RideDispatch.Application.Dispatch.AllocationRuns;
using RideDispatch.Application.Dispatch.Offers;
using RideDispatch.Application.RideRequests;
using RideDispatch.Domain.Dispatch;
using RideDispatch.Domain.RideRequests;

namespace RideDispatch.Application.Dispatch.Assignments;

public enum CreateAssignmentOutcome
{
    Success,
    OfferNotFound,
    OfferNotAccepted,
    ScheduledPickupElapsed,
    AssignmentAlreadyExists,
    DriverScheduleConflict,
}

public sealed record CreateAssignmentResult(CreateAssignmentOutcome Outcome, AssignmentView? Assignment = null);

public sealed class CreateAssignment(
    IOfferStore offerStore,
    IAllocationRunStore allocationRunStore,
    IRideRequestStore rideRequestStore,
    IAssignmentStore assignmentStore,
    TimeProvider timeProvider)
{
    public async Task<CreateAssignmentResult> ExecuteAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await offerStore.FindByIdReadOnlyAsync(offerId, cancellationToken);
        if (offer is null)
        {
            return new(CreateAssignmentOutcome.OfferNotFound);
        }

        if (offer.Status != OfferStatus.Accepted)
        {
            return new(CreateAssignmentOutcome.OfferNotAccepted);
        }

        var allocationRun = await allocationRunStore.FindByIdAsync(offer.AllocationRunId, cancellationToken)
            ?? throw new InvalidOperationException("An offer references a missing allocation run.");
        var rideRequest = await rideRequestStore.FindByIdAsync(allocationRun.RideRequestId, cancellationToken)
            ?? throw new InvalidOperationException("An allocation run references a missing ride request.");
        var currentUtcTime = timeProvider.GetUtcNow();
        var window = RideOccupancyWindow.From(rideRequest, currentUtcTime);
        if (rideRequest.Timing == RideTiming.Scheduled && window.StartsAt < currentUtcTime)
        {
            return new(CreateAssignmentOutcome.ScheduledPickupElapsed);
        }

        if (await assignmentStore.FindByOfferIdAsync(offer.Id, cancellationToken) is not null ||
            await assignmentStore.FindNonCancelledByRideRequestIdAsync(rideRequest.Id, cancellationToken) is not null)
        {
            return new(CreateAssignmentOutcome.AssignmentAlreadyExists);
        }

        if (await assignmentStore.HasDriverOverlapAsync(offer.DriverId, window.StartsAt, window.EndsAt, cancellationToken))
        {
            return new(CreateAssignmentOutcome.DriverScheduleConflict);
        }

        var assignment = Assignment.Create(offer.Id, rideRequest.Id, offer.DriverId,
            window.StartsAt, window.EndsAt - window.StartsAt, currentUtcTime);
        await assignmentStore.AddAsync(assignment, cancellationToken);
        try
        {
            await assignmentStore.SaveChangesAsync(cancellationToken);
        }
        catch (AssignmentAlreadyExistsException)
        {
            return new(CreateAssignmentOutcome.AssignmentAlreadyExists);
        }
        catch (AssignmentScheduleConflictException)
        {
            return new(CreateAssignmentOutcome.DriverScheduleConflict);
        }

        return new(CreateAssignmentOutcome.Success, AssignmentView.FromAssignment(assignment));
    }
}
